//! BROWSE endpoint for dense catalog of notebook contents.
//!
//! This module implements the BROWSE endpoint:
//! - GET /notebooks/{id}/browse - Returns catalog with optional query filtering
//!
//! The catalog provides a dense summary of notebook contents within a token budget,
//! allowing agents to quickly understand what's in a notebook without reading
//! every entry.
//!
//! Owned by: agent-browse (Task 3-3)

use axum::{
    Json, Router,
    extract::{Path, Query, State},
    routing::get,
};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_core::{ActivityContext, AuthorId, CausalPosition, Entry, EntryId, IntegrationCost};
use notebook_entropy::{
    catalog::{CatalogGenerator, ClusterSummary, DEFAULT_MAX_TOKENS},
    coherence::CoherenceSnapshot,
};
use notebook_store::{EntryQuery, StoreError};

use crate::error::{ApiError, ApiResult};
use crate::extract::{AuthorIdentity, require_scope};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

/// Query parameters for the BROWSE endpoint.
#[derive(Debug, Deserialize)]
pub struct BrowseParams {
    /// Optional search query to filter results.
    #[serde(default)]
    pub query: Option<String>,

    /// Maximum tokens for the response (default: 4000).
    #[serde(default)]
    pub max_tokens: Option<usize>,
}

/// Response for the BROWSE endpoint.
#[derive(Debug, Serialize)]
pub struct BrowseResponse {
    /// Cluster summaries ordered by significance.
    pub catalog: Vec<ClusterSummaryResponse>,

    /// Overall entropy measure for the notebook.
    pub notebook_entropy: f64,

    /// Total number of entries in the notebook.
    pub total_entries: u32,

    /// Number of entries matching the query (only present if query was provided).
    #[serde(skip_serializing_if = "Option::is_none")]
    pub query_matches: Option<usize>,
}

/// Summary of a single cluster for the response.
///
/// This is a simplified view of ClusterSummary for the API response.
#[derive(Debug, Serialize)]
pub struct ClusterSummaryResponse {
    /// Topic extracted from cluster keywords.
    pub topic: String,

    /// One-line extractive summary from cluster content.
    pub summary: String,

    /// Number of entries in this cluster.
    pub entry_count: u32,

    /// Total integration cost caused by entries in this cluster.
    pub cumulative_cost: f64,

    /// Entries since last cluster modification (higher = more stable).
    pub stability: u64,

    /// Representative entry IDs from this cluster.
    pub representative_entry_ids: Vec<Uuid>,
}

impl From<&ClusterSummary> for ClusterSummaryResponse {
    fn from(summary: &ClusterSummary) -> Self {
        Self {
            topic: summary.topic.clone(),
            summary: summary.summary.clone(),
            entry_count: summary.entry_count,
            cumulative_cost: summary.cumulative_cost,
            stability: summary.stability,
            representative_entry_ids: summary
                .representative_entry_ids
                .iter()
                .map(|id| id.0)
                .collect(),
        }
    }
}

// ============================================================================
// Route Handler
// ============================================================================

/// GET /notebooks/{id}/browse - Get a dense catalog of notebook contents.
///
/// Returns a catalog of cluster summaries within the specified token budget.
/// If a query is provided, filters to clusters containing matching entries.
///
/// # Query Parameters
///
/// - `query`: Optional search string to filter entries
/// - `max_tokens`: Maximum token budget (default: 4000)
///
/// # Response
///
/// - 200 OK: BrowseResponse with catalog
/// - 400 Bad Request: Invalid parameters
/// - 404 Not Found: Notebook not found
async fn browse_notebook(
    State(state): State<AppState>,
    identity: AuthorIdentity,
    Path(notebook_id): Path<Uuid>,
    Query(params): Query<BrowseParams>,
) -> ApiResult<Json<BrowseResponse>> {
    require_scope(&identity, "notebook:read", state.config())?;
    let store = state.store();

    // 1. Verify notebook exists
    store.get_notebook(notebook_id).await.map_err(|e| match e {
        StoreError::NotebookNotFound(id) => {
            ApiError::NotFound(format!("Notebook {} not found", id))
        }
        other => ApiError::Store(other),
    })?;

    // 2. Get all entries for the notebook
    let entry_query = EntryQuery {
        notebook_id: Some(notebook_id),
        topic: None,
        author_id: None,
        after_sequence: None,
        limit: None,
        newest_first: false,
    };

    let entry_rows = store.query_entries(&entry_query).await.map_err(|e| {
        tracing::error!(error = %e, "Failed to query entries");
        ApiError::Store(e)
    })?;

    // 3. Convert EntryRows to Entries
    let mut entries: Vec<Entry> = Vec::with_capacity(entry_rows.len());

    for row in &entry_rows {
        // Convert EntryRow to Entry using a simplified approach
        // We don't need full activity context for catalog generation
        let author_bytes: [u8; 32] =
            row.author_id.as_slice().try_into().map_err(|_| {
                ApiError::Internal("Invalid author_id length in database".to_string())
            })?;

        let integration_cost_json = row
            .parse_integration_cost()
            .map_err(|e| ApiError::Internal(format!("Failed to parse integration cost: {}", e)))?;

        let entry = Entry {
            id: EntryId::from_uuid(row.id),
            content: row.content.clone(),
            content_type: row.content_type.clone(),
            topic: row.topic.clone(),
            author: AuthorId::from_bytes(author_bytes),
            signature: row.signature.clone(),
            references: row
                .references
                .iter()
                .map(|u| EntryId::from_uuid(*u))
                .collect(),
            revision_of: row.revision_of.map(EntryId::from_uuid),
            causal_position: CausalPosition {
                sequence: row.sequence as u64,
                activity_context: ActivityContext {
                    entries_since_last_by_author: 0,
                    total_notebook_entries: entry_rows.len() as u32,
                    recent_entropy: 0.0,
                },
            },
            created: row.created,
            integration_cost: IntegrationCost::from(integration_cost_json),
        };

        entries.push(entry);
    }

    // 4. Build coherence snapshot from entries
    let max_sequence = entries
        .iter()
        .map(|e| e.causal_position.sequence)
        .max()
        .unwrap_or(0);
    let timestamp = CausalPosition {
        sequence: max_sequence,
        activity_context: ActivityContext {
            entries_since_last_by_author: 0,
            total_notebook_entries: entries.len() as u32,
            recent_entropy: 0.0,
        },
    };

    let mut snapshot = CoherenceSnapshot::new();
    snapshot.rebuild(&entries, timestamp);

    // 5. Handle search query if provided
    // Note: Full Tantivy search integration depends on Task 3-2 completion.
    // For now, we use simple text matching as a fallback.
    let (filtered_entry_ids, query_matches) = if let Some(ref query_str) = params.query {
        // Simple text-based search fallback
        // This will be replaced with Tantivy SearchIndex once Task 3-2 is fully integrated
        let query_lower = query_str.to_lowercase();
        let matching_ids: Vec<EntryId> = entries
            .iter()
            .filter(|entry| {
                // Match against content (for text types)
                let content_match = if entry.content_type.starts_with("text/") {
                    String::from_utf8_lossy(&entry.content)
                        .to_lowercase()
                        .contains(&query_lower)
                } else {
                    false
                };

                // Match against topic
                let topic_match = entry
                    .topic
                    .as_ref()
                    .map(|t| t.to_lowercase().contains(&query_lower))
                    .unwrap_or(false);

                content_match || topic_match
            })
            .map(|e| e.id)
            .collect();

        let count = matching_ids.len();
        tracing::debug!(
            query = %query_str,
            matches = count,
            "Simple text search completed"
        );
        (Some(matching_ids), Some(count))
    } else {
        (None, None)
    };

    // 6. Generate catalog
    let max_tokens = params.max_tokens.unwrap_or(DEFAULT_MAX_TOKENS);
    let generator = CatalogGenerator::with_max_tokens(max_tokens);
    let catalog = generator.generate(&snapshot, &entries, Some(max_tokens));

    // 7. Filter catalog by search results if query was provided
    let filtered_catalog = if let Some(ref matching_ids) = filtered_entry_ids {
        // Keep only clusters that contain at least one matching entry
        let matching_set: std::collections::HashSet<EntryId> =
            matching_ids.iter().copied().collect();

        catalog
            .clusters
            .iter()
            .filter(|cluster| {
                cluster
                    .representative_entry_ids
                    .iter()
                    .any(|id| matching_set.contains(id))
            })
            .map(ClusterSummaryResponse::from)
            .collect()
    } else {
        catalog
            .clusters
            .iter()
            .map(ClusterSummaryResponse::from)
            .collect()
    };

    // 8. Build response
    let response = BrowseResponse {
        catalog: filtered_catalog,
        notebook_entropy: catalog.notebook_entropy,
        total_entries: catalog.total_entries,
        query_matches,
    };

    tracing::info!(
        notebook_id = %notebook_id,
        total_entries = response.total_entries,
        clusters = response.catalog.len(),
        query_matches = ?response.query_matches,
        "Browse request completed"
    );

    Ok(Json(response))
}

/// Build browse routes.
pub fn routes() -> Router<AppState> {
    Router::new().route("/notebooks/{id}/browse", get(browse_notebook))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_browse_params_deserialize_empty() {
        let params: BrowseParams = serde_urlencoded::from_str("").unwrap();
        assert!(params.query.is_none());
        assert!(params.max_tokens.is_none());
    }

    #[test]
    fn test_browse_params_deserialize_query() {
        let params: BrowseParams = serde_urlencoded::from_str("query=machine+learning").unwrap();
        assert_eq!(params.query, Some("machine learning".to_string()));
        assert!(params.max_tokens.is_none());
    }

    #[test]
    fn test_browse_params_deserialize_max_tokens() {
        let params: BrowseParams = serde_urlencoded::from_str("max_tokens=2000").unwrap();
        assert!(params.query.is_none());
        assert_eq!(params.max_tokens, Some(2000));
    }

    #[test]
    fn test_browse_params_deserialize_full() {
        let params: BrowseParams =
            serde_urlencoded::from_str("query=test&max_tokens=1000").unwrap();
        assert_eq!(params.query, Some("test".to_string()));
        assert_eq!(params.max_tokens, Some(1000));
    }

    #[test]
    fn test_cluster_summary_response_from() {
        let summary = ClusterSummary {
            topic: "test topic".to_string(),
            summary: "Test summary.".to_string(),
            entry_count: 5,
            cumulative_cost: 1.5,
            stability: 10,
            representative_entry_ids: vec![EntryId::new()],
        };

        let response = ClusterSummaryResponse::from(&summary);

        assert_eq!(response.topic, "test topic");
        assert_eq!(response.summary, "Test summary.");
        assert_eq!(response.entry_count, 5);
        assert_eq!(response.cumulative_cost, 1.5);
        assert_eq!(response.stability, 10);
        assert_eq!(response.representative_entry_ids.len(), 1);
    }

    #[test]
    fn test_browse_response_serialize_without_query_matches() {
        let response = BrowseResponse {
            catalog: vec![],
            notebook_entropy: 5.5,
            total_entries: 100,
            query_matches: None,
        };

        let json = serde_json::to_string(&response).unwrap();

        // query_matches should be omitted when None
        assert!(!json.contains("query_matches"));
        assert!(json.contains("notebook_entropy"));
        assert!(json.contains("total_entries"));
    }

    #[test]
    fn test_browse_response_serialize_with_query_matches() {
        let response = BrowseResponse {
            catalog: vec![],
            notebook_entropy: 5.5,
            total_entries: 100,
            query_matches: Some(25),
        };

        let json = serde_json::to_string(&response).unwrap();

        assert!(json.contains("\"query_matches\":25"));
    }
}
