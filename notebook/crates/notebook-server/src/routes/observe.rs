//! OBSERVE endpoint for change notification.
//!
//! This module implements the OBSERVE endpoint that allows agents to see what
//! changed in a notebook since they last looked. Returns changes with their
//! integration costs and aggregate entropy for the observed period.
//!
//! Endpoint: GET /notebooks/{notebook_id}/observe?since={sequence}
//!
//! Owned by: agent-observe

use axum::{
    Json, Router,
    extract::{Path, Query, State},
    routing::get,
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_core::{AuthorId, IntegrationCost};
use notebook_store::{EntryQuery, EntryRow, StoreError};

use crate::error::{ApiError, ApiResult};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

/// Query parameters for the OBSERVE endpoint.
#[derive(Debug, Deserialize)]
pub struct ObserveParams {
    /// Sequence number to observe changes since (exclusive).
    /// If not provided, defaults to 0 (full sync - all entries).
    #[serde(default)]
    pub since: Option<u64>,
}

/// Response for the OBSERVE endpoint.
#[derive(Debug, Serialize)]
pub struct ObserveResponse {
    /// List of changes since the specified sequence.
    pub changes: Vec<ChangeEntry>,
    /// Aggregate entropy (sum of catalog_shift) for the observed period.
    pub notebook_entropy: f64,
    /// Current sequence number (highest sequence in the notebook).
    pub current_sequence: u64,
}

/// A single change entry in the observe response.
#[derive(Debug, Serialize)]
pub struct ChangeEntry {
    /// Entry ID.
    pub entry_id: Uuid,
    /// Operation type: "write" for new entries, "revise" for revisions.
    pub operation: &'static str,
    /// Author identity (hex-encoded 32-byte AuthorId).
    pub author: String,
    /// Optional topic/category.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub topic: Option<String>,
    /// Integration cost of this entry.
    pub integration_cost: IntegrationCost,
    /// Causal position of this entry.
    pub causal_position: CausalPositionSummary,
    /// Creation timestamp.
    pub created: DateTime<Utc>,
}

/// Summary of causal position for change entries.
#[derive(Debug, Serialize)]
pub struct CausalPositionSummary {
    /// Sequence number in the notebook.
    pub sequence: u64,
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Convert author_id bytes to hex string.
fn author_to_hex(author_id: &[u8]) -> String {
    author_id.iter().map(|b| format!("{:02x}", b)).collect()
}

/// Parse integration cost from EntryRow's JSONB field.
fn parse_integration_cost(row: &EntryRow) -> IntegrationCost {
    match row.parse_integration_cost() {
        Ok(cost_json) => IntegrationCost::from(cost_json),
        Err(_) => IntegrationCost::zero(),
    }
}

/// Convert an EntryRow to a ChangeEntry.
fn entry_row_to_change(row: &EntryRow) -> ChangeEntry {
    let operation = if row.revision_of.is_some() {
        "revise"
    } else {
        "write"
    };

    ChangeEntry {
        entry_id: row.id,
        operation,
        author: author_to_hex(&row.author_id),
        topic: row.topic.clone(),
        integration_cost: parse_integration_cost(row),
        causal_position: CausalPositionSummary {
            sequence: row.sequence as u64,
        },
        created: row.created,
    }
}

// ============================================================================
// Route Handler
// ============================================================================

/// GET /notebooks/{notebook_id}/observe - Observe changes since a sequence.
///
/// Returns all entries added to the notebook since the specified sequence
/// number, along with their integration costs and aggregate notebook entropy.
///
/// # Query Parameters
///
/// - `since`: Optional sequence number (exclusive). Defaults to 0 for full sync.
///
/// # Response
///
/// - 200 OK: `{ "changes": [...], "notebook_entropy": 15.5, "current_sequence": 150 }`
/// - 404 Not Found: Notebook not found
/// - 500 Internal Server Error: Database error
///
/// # Special Cases
///
/// - `since=0` or missing: Returns all entries (full sync)
/// - `since >= current_sequence`: Returns empty changes array
async fn observe_changes(
    State(state): State<AppState>,
    Path(notebook_id): Path<Uuid>,
    Query(params): Query<ObserveParams>,
) -> ApiResult<Json<ObserveResponse>> {
    let store = state.store();

    // Validate notebook exists
    store.get_notebook(notebook_id).await.map_err(|e| match e {
        StoreError::NotebookNotFound(id) => {
            ApiError::NotFound(format!("Notebook {} not found", id))
        }
        other => ApiError::Store(other),
    })?;

    // Get the since parameter (default to 0 for full sync)
    let since_sequence = params.since.unwrap_or(0) as i64;

    // Query entries with sequence > since
    let query = EntryQuery::new(notebook_id).after(since_sequence);
    let entries = store.query_entries(&query).await?;

    // Convert entries to changes and compute aggregate entropy
    let mut changes: Vec<ChangeEntry> = Vec::with_capacity(entries.len());
    let mut notebook_entropy: f64 = 0.0;
    let mut max_sequence: u64 = since_sequence as u64;

    for row in &entries {
        // Track max sequence
        if row.sequence as u64 > max_sequence {
            max_sequence = row.sequence as u64;
        }

        // Accumulate entropy (catalog_shift)
        let cost = parse_integration_cost(row);
        notebook_entropy += cost.catalog_shift;

        // Convert to change entry
        changes.push(entry_row_to_change(row));
    }

    // If no changes, we need to determine current_sequence from the database
    // Query for the maximum sequence in the notebook
    let current_sequence = if changes.is_empty() {
        // Check if there are any entries at all
        let all_query = EntryQuery::new(notebook_id);
        let all_entries = store.query_entries(&all_query).await?;
        all_entries
            .iter()
            .map(|e| e.sequence as u64)
            .max()
            .unwrap_or(0)
    } else {
        max_sequence
    };

    tracing::debug!(
        notebook_id = %notebook_id,
        since = since_sequence,
        changes_count = changes.len(),
        notebook_entropy = notebook_entropy,
        current_sequence = current_sequence,
        "OBSERVE completed"
    );

    Ok(Json(ObserveResponse {
        changes,
        notebook_entropy,
        current_sequence,
    }))
}

/// Build observe routes.
pub fn routes() -> Router<AppState> {
    Router::new().route("/notebooks/{id}/observe", get(observe_changes))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_observe_params_default() {
        let params: ObserveParams = serde_urlencoded::from_str("").unwrap();
        assert!(params.since.is_none());
    }

    #[test]
    fn test_observe_params_with_since() {
        let params: ObserveParams = serde_urlencoded::from_str("since=42").unwrap();
        assert_eq!(params.since, Some(42));
    }

    #[test]
    fn test_observe_params_since_zero() {
        let params: ObserveParams = serde_urlencoded::from_str("since=0").unwrap();
        assert_eq!(params.since, Some(0));
    }

    #[test]
    fn test_author_to_hex() {
        let author = [0u8; 32];
        let hex = author_to_hex(&author);
        assert_eq!(hex.len(), 64);
        assert!(hex.chars().all(|c| c == '0'));
    }

    #[test]
    fn test_author_to_hex_nonzero() {
        let mut author = [0u8; 32];
        author[0] = 0xab;
        author[31] = 0xcd;
        let hex = author_to_hex(&author);
        assert!(hex.starts_with("ab"));
        assert!(hex.ends_with("cd"));
    }

    #[test]
    fn test_observe_response_serialize() {
        let response = ObserveResponse {
            changes: vec![],
            notebook_entropy: 0.0,
            current_sequence: 0,
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("changes"));
        assert!(json.contains("notebook_entropy"));
        assert!(json.contains("current_sequence"));
    }

    #[test]
    fn test_change_entry_serialize_write() {
        let change = ChangeEntry {
            entry_id: Uuid::nil(),
            operation: "write",
            author: "0".repeat(64),
            topic: Some("test-topic".to_string()),
            integration_cost: IntegrationCost::zero(),
            causal_position: CausalPositionSummary { sequence: 1 },
            created: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&change).unwrap();
        assert!(json.contains("\"operation\":\"write\""));
        assert!(json.contains("\"topic\":\"test-topic\""));
    }

    #[test]
    fn test_change_entry_serialize_revise() {
        let change = ChangeEntry {
            entry_id: Uuid::nil(),
            operation: "revise",
            author: "0".repeat(64),
            topic: None,
            integration_cost: IntegrationCost::zero(),
            causal_position: CausalPositionSummary { sequence: 2 },
            created: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&change).unwrap();
        assert!(json.contains("\"operation\":\"revise\""));
        // topic should be omitted when None
        assert!(!json.contains("topic"));
    }

    #[test]
    fn test_change_entry_serialize_with_cost() {
        let change = ChangeEntry {
            entry_id: Uuid::nil(),
            operation: "write",
            author: "0".repeat(64),
            topic: None,
            integration_cost: IntegrationCost {
                entries_revised: 2,
                references_broken: 1,
                catalog_shift: 0.75,
                orphan: false,
            },
            causal_position: CausalPositionSummary { sequence: 5 },
            created: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&change).unwrap();
        assert!(json.contains("catalog_shift"));
        assert!(json.contains("0.75"));
    }

    #[test]
    fn test_causal_position_summary_serialize() {
        let pos = CausalPositionSummary { sequence: 42 };
        let json = serde_json::to_string(&pos).unwrap();
        assert_eq!(json, r#"{"sequence":42}"#);
    }
}
