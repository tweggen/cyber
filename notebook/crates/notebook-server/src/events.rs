//! Event broadcasting for real-time notifications.
//!
//! This module provides a pub/sub mechanism for broadcasting notebook events
//! to connected SSE clients. Events are published when entries are created
//! or revised, allowing clients to receive real-time updates.
//!
//! # Architecture
//!
//! - Uses `tokio::sync::broadcast` for multi-subscriber pub/sub
//! - One channel per notebook (created lazily on first subscription)
//! - Channels are cleaned up when all subscribers disconnect
//!
//! # Event Types
//!
//! - `entry`: Published on WRITE/REVISE operations
//! - `heartbeat`: Sent periodically to keep connections alive
//! - `catchup`: Sent when a subscriber falls behind
//!
//! Owned by: agent-events

use std::collections::HashMap;
use std::sync::Arc;

use chrono::{DateTime, Utc};
use serde::Serialize;
use tokio::sync::{broadcast, RwLock};
use uuid::Uuid;

use notebook_core::IntegrationCost;

/// Default channel capacity for broadcast channels.
pub const DEFAULT_CHANNEL_CAPACITY: usize = 256;

/// Heartbeat interval in seconds.
pub const HEARTBEAT_INTERVAL_SECS: u64 = 30;

// ============================================================================
// Event Types
// ============================================================================

/// An event that can be broadcast to subscribers.
#[derive(Debug, Clone, Serialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum NotebookEvent {
    /// An entry was created or revised.
    Entry(EntryEvent),
    /// Periodic heartbeat to keep connection alive.
    Heartbeat(HeartbeatEvent),
    /// Client fell behind and should sync via OBSERVE.
    Catchup(CatchupEvent),
}

/// Event data for entry creation/revision.
#[derive(Debug, Clone, Serialize)]
pub struct EntryEvent {
    /// The entry ID.
    pub entry_id: Uuid,
    /// Operation type: "write" or "revise".
    pub operation: String,
    /// Integration cost of the entry.
    pub integration_cost: IntegrationCost,
    /// The sequence number of the entry.
    pub sequence: u64,
    /// Timestamp of the event.
    pub timestamp: DateTime<Utc>,
}

/// Heartbeat event data.
#[derive(Debug, Clone, Serialize)]
pub struct HeartbeatEvent {
    /// Current timestamp.
    pub timestamp: DateTime<Utc>,
}

/// Catchup event sent when subscriber falls behind.
#[derive(Debug, Clone, Serialize)]
pub struct CatchupEvent {
    /// Number of events missed.
    pub events_missed: u64,
    /// Current sequence to sync from.
    pub current_sequence: u64,
    /// Timestamp of the catchup event.
    pub timestamp: DateTime<Utc>,
}

// ============================================================================
// Event Broadcaster
// ============================================================================

/// Manages broadcast channels for notebook events.
///
/// Each notebook has its own broadcast channel. Channels are created lazily
/// when the first subscriber connects and cleaned up when all subscribers
/// disconnect.
#[derive(Debug, Clone)]
pub struct EventBroadcaster {
    /// Map of notebook_id -> broadcast sender.
    channels: Arc<RwLock<HashMap<Uuid, broadcast::Sender<NotebookEvent>>>>,
    /// Channel capacity for new channels.
    capacity: usize,
}

impl Default for EventBroadcaster {
    fn default() -> Self {
        Self::new()
    }
}

impl EventBroadcaster {
    /// Create a new event broadcaster with default capacity.
    pub fn new() -> Self {
        Self {
            channels: Arc::new(RwLock::new(HashMap::new())),
            capacity: DEFAULT_CHANNEL_CAPACITY,
        }
    }

    /// Create a new event broadcaster with custom capacity.
    pub fn with_capacity(capacity: usize) -> Self {
        Self {
            channels: Arc::new(RwLock::new(HashMap::new())),
            capacity,
        }
    }

    /// Subscribe to events for a notebook.
    ///
    /// Creates the channel if it doesn't exist.
    /// Returns a receiver that can be used to receive events.
    pub async fn subscribe(&self, notebook_id: Uuid) -> broadcast::Receiver<NotebookEvent> {
        // First try to get existing channel
        {
            let channels = self.channels.read().await;
            if let Some(sender) = channels.get(&notebook_id) {
                return sender.subscribe();
            }
        }

        // Create new channel
        let mut channels = self.channels.write().await;
        // Check again in case another task created it
        if let Some(sender) = channels.get(&notebook_id) {
            return sender.subscribe();
        }

        let (sender, receiver) = broadcast::channel(self.capacity);
        channels.insert(notebook_id, sender);

        tracing::debug!(
            notebook_id = %notebook_id,
            capacity = self.capacity,
            "Created event channel for notebook"
        );

        receiver
    }

    /// Publish an event to all subscribers of a notebook.
    ///
    /// Returns the number of receivers that received the event,
    /// or None if no channel exists for this notebook.
    pub async fn publish(&self, notebook_id: Uuid, event: NotebookEvent) -> Option<usize> {
        let channels = self.channels.read().await;
        if let Some(sender) = channels.get(&notebook_id) {
            match sender.send(event) {
                Ok(count) => {
                    tracing::trace!(
                        notebook_id = %notebook_id,
                        receivers = count,
                        "Published event to subscribers"
                    );
                    Some(count)
                }
                Err(_) => {
                    // No receivers - this is fine, channel will be cleaned up
                    tracing::trace!(
                        notebook_id = %notebook_id,
                        "No subscribers for event"
                    );
                    Some(0)
                }
            }
        } else {
            None
        }
    }

    /// Publish an entry event (convenience method).
    pub async fn publish_entry(
        &self,
        notebook_id: Uuid,
        entry_id: Uuid,
        operation: &str,
        integration_cost: IntegrationCost,
        sequence: u64,
    ) -> Option<usize> {
        let event = NotebookEvent::Entry(EntryEvent {
            entry_id,
            operation: operation.to_string(),
            integration_cost,
            sequence,
            timestamp: Utc::now(),
        });
        self.publish(notebook_id, event).await
    }

    /// Get the number of active channels.
    pub async fn channel_count(&self) -> usize {
        self.channels.read().await.len()
    }

    /// Get the number of subscribers for a notebook.
    pub async fn subscriber_count(&self, notebook_id: Uuid) -> usize {
        let channels = self.channels.read().await;
        channels
            .get(&notebook_id)
            .map(|s| s.receiver_count())
            .unwrap_or(0)
    }

    /// Clean up channels with no subscribers.
    ///
    /// This can be called periodically to free up resources.
    pub async fn cleanup_empty_channels(&self) -> usize {
        let mut channels = self.channels.write().await;
        let before = channels.len();
        channels.retain(|id, sender| {
            let has_receivers = sender.receiver_count() > 0;
            if !has_receivers {
                tracing::debug!(
                    notebook_id = %id,
                    "Cleaning up empty event channel"
                );
            }
            has_receivers
        });
        before - channels.len()
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_broadcaster_subscribe() {
        let broadcaster = EventBroadcaster::new();
        let notebook_id = Uuid::new_v4();

        let _receiver = broadcaster.subscribe(notebook_id).await;
        assert_eq!(broadcaster.channel_count().await, 1);
        assert_eq!(broadcaster.subscriber_count(notebook_id).await, 1);
    }

    #[tokio::test]
    async fn test_broadcaster_multiple_subscribers() {
        let broadcaster = EventBroadcaster::new();
        let notebook_id = Uuid::new_v4();

        let _r1 = broadcaster.subscribe(notebook_id).await;
        let _r2 = broadcaster.subscribe(notebook_id).await;
        let _r3 = broadcaster.subscribe(notebook_id).await;

        assert_eq!(broadcaster.channel_count().await, 1);
        assert_eq!(broadcaster.subscriber_count(notebook_id).await, 3);
    }

    #[tokio::test]
    async fn test_broadcaster_publish() {
        let broadcaster = EventBroadcaster::new();
        let notebook_id = Uuid::new_v4();

        let mut receiver = broadcaster.subscribe(notebook_id).await;

        let count = broadcaster
            .publish_entry(
                notebook_id,
                Uuid::new_v4(),
                "write",
                IntegrationCost::zero(),
                1,
            )
            .await;

        assert_eq!(count, Some(1));

        let event = receiver.recv().await.unwrap();
        match event {
            NotebookEvent::Entry(e) => {
                assert_eq!(e.operation, "write");
                assert_eq!(e.sequence, 1);
            }
            _ => panic!("Expected Entry event"),
        }
    }

    #[tokio::test]
    async fn test_broadcaster_publish_no_channel() {
        let broadcaster = EventBroadcaster::new();
        let notebook_id = Uuid::new_v4();

        let count = broadcaster
            .publish_entry(
                notebook_id,
                Uuid::new_v4(),
                "write",
                IntegrationCost::zero(),
                1,
            )
            .await;

        assert_eq!(count, None);
    }

    #[tokio::test]
    async fn test_broadcaster_cleanup() {
        let broadcaster = EventBroadcaster::new();
        let notebook_id = Uuid::new_v4();

        {
            let _receiver = broadcaster.subscribe(notebook_id).await;
            assert_eq!(broadcaster.channel_count().await, 1);
        }
        // receiver dropped

        let cleaned = broadcaster.cleanup_empty_channels().await;
        assert_eq!(cleaned, 1);
        assert_eq!(broadcaster.channel_count().await, 0);
    }

    #[tokio::test]
    async fn test_event_serialization() {
        let event = NotebookEvent::Entry(EntryEvent {
            entry_id: Uuid::nil(),
            operation: "write".to_string(),
            integration_cost: IntegrationCost::zero(),
            sequence: 42,
            timestamp: Utc::now(),
        });

        let json = serde_json::to_string(&event).unwrap();
        assert!(json.contains("\"type\":\"entry\""));
        assert!(json.contains("\"operation\":\"write\""));
        assert!(json.contains("\"sequence\":42"));
    }

    #[tokio::test]
    async fn test_heartbeat_event_serialization() {
        let event = NotebookEvent::Heartbeat(HeartbeatEvent {
            timestamp: Utc::now(),
        });

        let json = serde_json::to_string(&event).unwrap();
        assert!(json.contains("\"type\":\"heartbeat\""));
        assert!(json.contains("timestamp"));
    }

    #[tokio::test]
    async fn test_catchup_event_serialization() {
        let event = NotebookEvent::Catchup(CatchupEvent {
            events_missed: 100,
            current_sequence: 150,
            timestamp: Utc::now(),
        });

        let json = serde_json::to_string(&event).unwrap();
        assert!(json.contains("\"type\":\"catchup\""));
        assert!(json.contains("\"events_missed\":100"));
        assert!(json.contains("\"current_sequence\":150"));
    }
}
