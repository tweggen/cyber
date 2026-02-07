//! Server-Sent Events (SSE) endpoint for real-time notifications.
//!
//! This module implements the SSE endpoint that allows clients to subscribe
//! to real-time notebook events instead of polling the OBSERVE endpoint.
//!
//! Endpoint: GET /notebooks/{notebook_id}/events
//!
//! # Event Types
//!
//! - `entry`: Published when an entry is created or revised
//! - `heartbeat`: Sent every 30 seconds to keep the connection alive
//! - `catchup`: Sent when the client falls behind and needs to sync via OBSERVE
//!
//! # Example
//!
//! ```text
//! event: entry
//! data: {"entry_id": "...", "operation": "write", "integration_cost": {...}}
//!
//! event: heartbeat
//! data: {"timestamp": "2024-01-01T00:00:00Z"}
//! ```
//!
//! Owned by: agent-events

use std::convert::Infallible;
use std::time::Duration;

use axum::{
    Router,
    extract::{Path, State},
    response::sse::{Event, KeepAlive, Sse},
    routing::get,
};
use chrono::Utc;
use futures::stream::{self, Stream, StreamExt};
use tokio::sync::broadcast::error::RecvError;
use uuid::Uuid;

use notebook_store::StoreError;

use crate::error::ApiError;
use crate::events::{CatchupEvent, HEARTBEAT_INTERVAL_SECS, HeartbeatEvent, NotebookEvent};
use crate::state::AppState;

// ============================================================================
// SSE Endpoint
// ============================================================================

/// GET /notebooks/{notebook_id}/events - Subscribe to real-time events.
///
/// Returns a Server-Sent Events stream that emits events when entries are
/// created or revised in the notebook. Heartbeats are sent every 30 seconds
/// to keep the connection alive.
///
/// # Response
///
/// - 200 OK: SSE stream (Content-Type: text/event-stream)
/// - 404 Not Found: Notebook not found
///
/// # Event Format
///
/// ```text
/// event: entry
/// data: {"type":"entry","entry_id":"...","operation":"write","integration_cost":{...},"sequence":42,"timestamp":"..."}
///
/// event: heartbeat
/// data: {"type":"heartbeat","timestamp":"..."}
///
/// event: catchup
/// data: {"type":"catchup","events_missed":100,"current_sequence":150,"timestamp":"..."}
/// ```
///
/// # Backpressure
///
/// If a client falls behind (channel buffer overflows), a `catchup` event is
/// sent indicating how many events were missed. The client should then use
/// the OBSERVE endpoint to sync up.
async fn subscribe_events(
    State(state): State<AppState>,
    Path(notebook_id): Path<Uuid>,
) -> Result<Sse<impl Stream<Item = Result<Event, Infallible>>>, ApiError> {
    // Validate notebook exists
    state
        .store()
        .get_notebook(notebook_id)
        .await
        .map_err(|e| match e {
            StoreError::NotebookNotFound(id) => {
                ApiError::NotFound(format!("Notebook {} not found", id))
            }
            other => ApiError::Store(other),
        })?;

    // Get broadcaster from state
    let broadcaster = state.broadcaster();

    // Subscribe to events
    let mut receiver = broadcaster.subscribe(notebook_id).await;

    tracing::info!(
        notebook_id = %notebook_id,
        "Client subscribed to SSE events"
    );

    // Create the event stream
    let stream = stream::unfold(
        (receiver, notebook_id, 0u64),
        move |(mut rx, nb_id, mut last_sequence)| async move {
            loop {
                match rx.recv().await {
                    Ok(event) => {
                        // Update last_sequence for entry events
                        if let NotebookEvent::Entry(ref e) = event {
                            last_sequence = e.sequence;
                        }

                        let event_type = match &event {
                            NotebookEvent::Entry(_) => "entry",
                            NotebookEvent::Heartbeat(_) => "heartbeat",
                            NotebookEvent::Catchup(_) => "catchup",
                        };

                        match serde_json::to_string(&event) {
                            Ok(data) => {
                                let sse_event = Event::default().event(event_type).data(data);
                                return Some((Ok(sse_event), (rx, nb_id, last_sequence)));
                            }
                            Err(e) => {
                                tracing::error!(
                                    error = %e,
                                    "Failed to serialize event"
                                );
                                continue;
                            }
                        }
                    }
                    Err(RecvError::Lagged(count)) => {
                        // Client fell behind - send catchup event
                        tracing::warn!(
                            notebook_id = %nb_id,
                            events_missed = count,
                            "SSE client lagged, sending catchup event"
                        );

                        let catchup = NotebookEvent::Catchup(CatchupEvent {
                            events_missed: count,
                            current_sequence: last_sequence,
                            timestamp: Utc::now(),
                        });

                        match serde_json::to_string(&catchup) {
                            Ok(data) => {
                                let sse_event = Event::default().event("catchup").data(data);
                                return Some((Ok(sse_event), (rx, nb_id, last_sequence)));
                            }
                            Err(e) => {
                                tracing::error!(
                                    error = %e,
                                    "Failed to serialize catchup event"
                                );
                                continue;
                            }
                        }
                    }
                    Err(RecvError::Closed) => {
                        // Channel closed - end stream
                        tracing::debug!(
                            notebook_id = %nb_id,
                            "Event channel closed, ending SSE stream"
                        );
                        return None;
                    }
                }
            }
        },
    );

    // Configure keep-alive with heartbeat
    let keep_alive = KeepAlive::new()
        .interval(Duration::from_secs(HEARTBEAT_INTERVAL_SECS))
        .event(
            Event::default().event("heartbeat").data(
                serde_json::to_string(&NotebookEvent::Heartbeat(HeartbeatEvent {
                    timestamp: Utc::now(),
                }))
                .unwrap_or_else(|_| r#"{"type":"heartbeat","timestamp":"unknown"}"#.to_string()),
            ),
        );

    Ok(Sse::new(stream).keep_alive(keep_alive))
}

/// Build SSE event routes.
pub fn routes() -> Router<AppState> {
    Router::new().route("/notebooks/{id}/events", get(subscribe_events))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_heartbeat_interval() {
        assert_eq!(HEARTBEAT_INTERVAL_SECS, 30);
    }
}
