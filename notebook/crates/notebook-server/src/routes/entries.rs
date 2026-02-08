//! Entry routes for the Knowledge Exchange Platform.
//!
//! This module implements the entry-related HTTP endpoints:
//! - POST /notebooks/{id}/entries - Create a new entry
//! - PUT /notebooks/{id}/entries/{entry_id} - Revise an entry
//! - GET /notebooks/{id}/entries/{entry_id} - Get an entry
//!
//! Owned by: agent-revise (REVISE endpoint), agent-write (WRITE endpoint), agent-read (READ endpoint)

use axum::{
    Json, Router,
    extract::{Path, Query, State},
    http::{HeaderMap, HeaderValue, StatusCode},
    routing::{post, put},
};
use base64::Engine;
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_core::{AuthorId, CausalPosition, Entry, EntryId, IntegrationCost, NotebookId};
use notebook_store::{
    CausalPositionService, IntegrationCostJson, NewEntry, Repository, StoreEntryInput, StoreError,
};

use crate::error::{ApiError, ApiResult};
use crate::extract::{AuthorIdentity, require_scope};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

/// Request body for creating a new entry.
#[derive(Debug, Deserialize)]
pub struct CreateEntryRequest {
    /// Content as a string. For text content_types, used as-is.
    /// For binary content_types, should be base64 encoded.
    pub content: String,

    /// MIME-like content type (e.g., "text/plain", "application/json").
    pub content_type: String,

    /// Optional topic/category for the entry.
    #[serde(default)]
    pub topic: Option<String>,

    /// References to other entries (UUIDs).
    #[serde(default)]
    pub references: Vec<Uuid>,
}

/// Response for successful entry creation.
#[derive(Debug, Serialize)]
pub struct CreateEntryResponse {
    /// The assigned entry ID.
    pub entry_id: Uuid,

    /// The assigned causal position.
    pub causal_position: CausalPosition,

    /// Integration cost (placeholder zeros for Phase 1).
    pub integration_cost: IntegrationCost,
}

/// Request body for revising an entry.
#[derive(Debug, Deserialize)]
pub struct ReviseRequest {
    /// The new content for the revision.
    pub content: String,
    /// Optional reason for the revision (for audit, not stored in entry).
    #[serde(default)]
    pub reason: Option<String>,
}

/// Response for a successful revision.
#[derive(Debug, Serialize)]
pub struct ReviseResponse {
    /// The ID of the newly created revision entry.
    pub revision_id: EntryId,
    /// The causal position assigned to the revision.
    pub causal_position: CausalPosition,
    /// The integration cost of the revision (placeholder zeros).
    pub integration_cost: IntegrationCost,
}

// ============================================================================
// Request/Response Types for READ endpoint
// ============================================================================

/// Query parameters for GET entry endpoint.
#[derive(Debug, Deserialize)]
pub struct GetEntryParams {
    /// Optional revision number (0 = current, 1 = first revision, etc.)
    pub revision: Option<u32>,
}

/// Response for GET /notebooks/{notebook_id}/entries/{entry_id}
#[derive(Debug, Serialize)]
pub struct ReadEntryResponse {
    /// The full entry data.
    pub entry: EntryResponse,
    /// Revision chain (entries that revise this entry).
    pub revisions: Vec<EntrySummary>,
    /// Entries that this entry references.
    pub references: Vec<EntrySummary>,
    /// Entries that reference this entry.
    pub referenced_by: Vec<EntrySummary>,
}

/// Full entry data for the response.
#[derive(Debug, Serialize)]
pub struct EntryResponse {
    /// Entry ID.
    pub id: EntryId,
    /// Content - string if text/*, base64 encoded otherwise.
    pub content: EntryContent,
    /// MIME content type.
    pub content_type: String,
    /// Optional topic/category.
    pub topic: Option<String>,
    /// Author identity (hex-encoded).
    pub author: AuthorId,
    /// References to other entries.
    pub references: Vec<EntryId>,
    /// If this revises another entry.
    pub revision_of: Option<EntryId>,
    /// Causal position in the notebook.
    pub causal_position: CausalPositionResponse,
    /// Creation timestamp.
    pub created: DateTime<Utc>,
    /// System-computed integration cost.
    pub integration_cost: IntegrationCost,
}

/// Causal position in response format.
#[derive(Debug, Serialize)]
pub struct CausalPositionResponse {
    /// Sequence number in the notebook.
    pub sequence: u64,
    /// Activity context at creation time.
    pub activity_context: ActivityContextResponse,
}

/// Activity context in response format.
#[derive(Debug, Serialize)]
pub struct ActivityContextResponse {
    /// Entries since author's last entry.
    pub entries_since_last_by_author: u32,
    /// Total entries in notebook at creation time.
    pub total_notebook_entries: u32,
    /// Recent entropy measure.
    pub recent_entropy: f64,
}

/// Entry content that can be either text or base64-encoded binary.
#[derive(Debug, Serialize)]
#[serde(untagged)]
pub enum EntryContent {
    /// Text content (for text/* content types).
    Text(String),
    /// Base64-encoded binary content.
    Binary {
        /// Base64-encoded data.
        data: String,
        /// Encoding type (always "base64").
        encoding: &'static str,
    },
}

/// Summary of an entry for references and revisions lists.
#[derive(Debug, Serialize)]
pub struct EntrySummary {
    /// Entry ID.
    pub id: EntryId,
    /// Optional topic.
    pub topic: Option<String>,
    /// Author identity.
    pub author: AuthorId,
    /// Creation timestamp.
    pub created: DateTime<Utc>,
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Determine if content should be treated as binary based on content_type.
/// Binary content is expected to be base64 encoded in the request.
fn is_binary_content_type(content_type: &str) -> bool {
    // Text-based types that should NOT be base64 decoded
    let text_types = [
        "text/",
        "application/json",
        "application/xml",
        "application/javascript",
        "application/x-www-form-urlencoded",
    ];

    !text_types.iter().any(|t| content_type.starts_with(t))
}

/// Get content bytes from request, decoding base64 if content is binary.
fn get_content_bytes(request: &CreateEntryRequest) -> Result<Vec<u8>, ApiError> {
    if is_binary_content_type(&request.content_type) {
        // Binary content - decode from base64
        base64::engine::general_purpose::STANDARD
            .decode(&request.content)
            .map_err(|e| ApiError::BadRequest(format!("Invalid base64 content: {}", e)))
    } else {
        // Text content - use as-is
        Ok(request.content.as_bytes().to_vec())
    }
}

/// Encode entry content based on content type for READ response.
///
/// If content_type starts with "text/", attempts to decode as UTF-8 string.
/// Otherwise, returns base64-encoded binary.
fn encode_content(content: &[u8], content_type: &str) -> EntryContent {
    if content_type.starts_with("text/") {
        // Try to decode as UTF-8
        match std::str::from_utf8(content) {
            Ok(s) => EntryContent::Text(s.to_string()),
            Err(_) => {
                // Invalid UTF-8, fall back to base64
                let encoded = base64::engine::general_purpose::STANDARD.encode(content);
                EntryContent::Binary {
                    data: encoded,
                    encoding: "base64",
                }
            }
        }
    } else {
        // Binary content, base64 encode
        let encoded = base64::engine::general_purpose::STANDARD.encode(content);
        EntryContent::Binary {
            data: encoded,
            encoding: "base64",
        }
    }
}

/// Convert a notebook_core::Entry to EntrySummary.
fn entry_to_summary(entry: &Entry) -> EntrySummary {
    EntrySummary {
        id: entry.id,
        topic: entry.topic.clone(),
        author: entry.author,
        created: entry.created,
    }
}

/// Convert a notebook_core::Entry to full EntryResponse.
fn entry_to_response(entry: &Entry) -> EntryResponse {
    EntryResponse {
        id: entry.id,
        content: encode_content(&entry.content, &entry.content_type),
        content_type: entry.content_type.clone(),
        topic: entry.topic.clone(),
        author: entry.author,
        references: entry.references.clone(),
        revision_of: entry.revision_of,
        causal_position: CausalPositionResponse {
            sequence: entry.causal_position.sequence,
            activity_context: ActivityContextResponse {
                entries_since_last_by_author: entry
                    .causal_position
                    .activity_context
                    .entries_since_last_by_author,
                total_notebook_entries: entry
                    .causal_position
                    .activity_context
                    .total_notebook_entries,
                recent_entropy: entry.causal_position.activity_context.recent_entropy,
            },
        },
        created: entry.created,
        integration_cost: entry.integration_cost,
    }
}

// ============================================================================
// Route Handlers
// ============================================================================

/// POST /notebooks/:id/entries - Create a new entry.
///
/// Creates a new entry in the specified notebook with the given content.
/// Validates that:
/// - The notebook exists
/// - All referenced entries exist
///
/// # Request
///
/// Body: `{ "content": "...", "content_type": "text/plain", "topic": "optional", "references": [] }`
///
/// For binary content, the content field should be base64 encoded.
///
/// # Response
///
/// - 201 Created: `{ "entry_id": "...", "causal_position": {...}, "integration_cost": {...} }`
/// - 400 Bad Request: Invalid request body or invalid references
/// - 404 Not Found: Notebook not found
/// - 500 Internal Server Error: Storage failure
async fn create_entry(
    State(state): State<AppState>,
    identity: AuthorIdentity,
    Path(notebook_id): Path<Uuid>,
    Json(request): Json<CreateEntryRequest>,
) -> ApiResult<(StatusCode, HeaderMap, Json<CreateEntryResponse>)> {
    require_scope(&identity, "notebook:write", state.config())?;
    let author_id = identity.author_id;
    let store = state.store();
    let pool = store.pool();

    // 1. Validate notebook exists
    store.get_notebook(notebook_id).await.map_err(|e| match e {
        StoreError::NotebookNotFound(id) => {
            ApiError::NotFound(format!("Notebook {} not found", id))
        }
        other => ApiError::Store(other),
    })?;

    // 2. Validate all references exist
    for ref_id in &request.references {
        if !store.entry_exists(*ref_id).await? {
            return Err(ApiError::BadRequest(format!(
                "Referenced entry {} does not exist",
                ref_id
            )));
        }
    }

    // 3. Get content bytes (decode base64 if binary)
    let content = get_content_bytes(&request)?;

    // 4. Assign causal position
    let causal_position =
        CausalPositionService::assign_position(pool, NotebookId::from_uuid(notebook_id), author_id)
            .await
            .map_err(|e| match e {
                StoreError::NotebookNotFound(id) => {
                    ApiError::NotFound(format!("Notebook {} not found", id))
                }
                other => ApiError::Store(other),
            })?;

    // 6. Build Entry for cost computation
    let entry_id = Uuid::new_v4();
    let references: Vec<EntryId> = request
        .references
        .iter()
        .map(|&u| EntryId::from_uuid(u))
        .collect();
    let temp_entry = Entry {
        id: EntryId::from_uuid(entry_id),
        content: content.clone(),
        content_type: request.content_type.clone(),
        topic: request.topic.clone(),
        author: author_id,
        signature: vec![0u8; 64],
        references: references.clone(),
        revision_of: None,
        causal_position,
        created: Utc::now(),
        integration_cost: IntegrationCost::zero(),
    };

    // 7. Compute integration cost using entropy engine
    let (integration_cost, cost_computed) = {
        let mut engine = state.engine().lock().await;
        match engine.compute_cost(&temp_entry, NotebookId::from_uuid(notebook_id)) {
            Ok(cost) => {
                tracing::debug!(
                    entry_id = %entry_id,
                    entries_revised = cost.entries_revised,
                    catalog_shift = cost.catalog_shift,
                    orphan = cost.orphan,
                    "Integration cost computed"
                );
                (cost, true)
            }
            Err(e) => {
                tracing::warn!(
                    entry_id = %entry_id,
                    error = %e,
                    "Failed to compute integration cost, using zeros"
                );
                (IntegrationCost::zero(), false)
            }
        }
    };

    // 8. Build NewEntry with computed cost
    let cost_json = IntegrationCostJson {
        entries_revised: integration_cost.entries_revised,
        references_broken: integration_cost.references_broken,
        catalog_shift: integration_cost.catalog_shift,
        orphan: integration_cost.orphan,
    };
    let new_entry = NewEntry::builder(notebook_id, *author_id.as_bytes())
        .id(entry_id)
        .content(content)
        .content_type(request.content_type)
        .topic(request.topic)
        .signature(vec![0u8; 64]) // Placeholder signature (Phase 1)
        .references(request.references)
        .integration_cost(cost_json)
        .build();

    // 9. Store the entry
    store.insert_entry(&new_entry).await.map_err(|e| match e {
        StoreError::InvalidReference(id) => {
            ApiError::BadRequest(format!("Referenced entry {} does not exist", id))
        }
        StoreError::InvalidRevision(id) => {
            ApiError::BadRequest(format!("Revision target {} does not exist", id))
        }
        other => ApiError::Store(other),
    })?;

    tracing::info!(
        entry_id = %entry_id,
        notebook_id = %notebook_id,
        sequence = causal_position.sequence,
        "Entry created successfully"
    );

    // 10. Publish event to SSE subscribers
    let broadcaster = state.broadcaster();
    if let Some(subscriber_count) = broadcaster
        .publish_entry(
            notebook_id,
            entry_id,
            "write",
            integration_cost,
            causal_position.sequence,
        )
        .await
    {
        tracing::debug!(
            entry_id = %entry_id,
            subscribers = subscriber_count,
            "Published write event to SSE subscribers"
        );
    }

    // 11. Build response with headers
    let mut headers = HeaderMap::new();
    headers.insert(
        "X-Integration-Cost-Computed",
        HeaderValue::from_static(if cost_computed { "true" } else { "false" }),
    );

    let response = CreateEntryResponse {
        entry_id,
        causal_position,
        integration_cost,
    };

    Ok((StatusCode::CREATED, headers, Json(response)))
}

/// PUT /notebooks/:id/entries/:entry_id - Revise an entry.
///
/// Creates a new entry that is a revision of the specified entry.
/// The original entry is preserved; the new entry has `revision_of` set
/// to the original entry's ID.
///
/// # Request
///
/// - Body: `{ "content": "new content", "reason": "optional reason" }`
///
/// # Response
///
/// - 200 OK: `{ "revision_id": "...", "causal_position": {...}, "integration_cost": {...} }`
/// - 400 Bad Request: Invalid request body
/// - 404 Not Found: Notebook or entry not found
/// - 500 Internal Server Error: Storage failure
async fn revise_entry(
    State(state): State<AppState>,
    identity: AuthorIdentity,
    Path((notebook_id, entry_id)): Path<(Uuid, Uuid)>,
    Json(request): Json<ReviseRequest>,
) -> ApiResult<(HeaderMap, Json<ReviseResponse>)> {
    require_scope(&identity, "notebook:write", state.config())?;
    let author_id = identity.author_id;
    // Log the revision reason if provided (for audit purposes)
    if let Some(ref reason) = request.reason {
        tracing::info!(
            notebook_id = %notebook_id,
            entry_id = %entry_id,
            reason = %reason,
            "Revising entry"
        );
    }

    // Create a Repository from the store
    let repo = Repository::new(state.store().clone());

    // Wrap notebook_id and entry_id in typed wrappers
    let notebook_id = NotebookId::from_uuid(notebook_id);
    let entry_id = EntryId::from_uuid(entry_id);

    // Fetch the original entry
    let original = repo.get_entry(entry_id).await.map_err(|e| {
        tracing::warn!(error = %e, "Failed to fetch original entry");
        e
    })?;

    // Assign causal position for the new revision
    let causal_position =
        CausalPositionService::assign_position(state.store().pool(), notebook_id, author_id)
            .await
            .map_err(|e| {
                tracing::error!(error = %e, "Failed to assign causal position");
                e
            })?;

    // Create the revision entry (with placeholder cost for now)
    let revision_id = EntryId::new();
    let revision_entry = Entry {
        id: revision_id,
        content: request.content.into_bytes(),
        content_type: original.content_type.clone(),
        topic: original.topic.clone(),
        author: author_id,
        signature: vec![0u8; 64], // Placeholder signature
        references: original.references.clone(),
        revision_of: Some(entry_id),
        causal_position,
        created: Utc::now(),
        integration_cost: IntegrationCost::zero(),
    };

    // Compute integration cost using entropy engine
    let (integration_cost, cost_computed) = {
        let mut engine = state.engine().lock().await;
        match engine.compute_cost(&revision_entry, notebook_id) {
            Ok(cost) => {
                tracing::debug!(
                    revision_id = %revision_id,
                    entries_revised = cost.entries_revised,
                    catalog_shift = cost.catalog_shift,
                    orphan = cost.orphan,
                    "Integration cost computed for revision"
                );
                (cost, true)
            }
            Err(e) => {
                tracing::warn!(
                    revision_id = %revision_id,
                    error = %e,
                    "Failed to compute integration cost for revision, using zeros"
                );
                (IntegrationCost::zero(), false)
            }
        }
    };

    // Update entry with computed cost
    let revision_entry = Entry {
        integration_cost,
        ..revision_entry
    };

    // Store the revision entry
    let input = StoreEntryInput {
        entry: revision_entry,
        notebook_id,
    };

    repo.store_entry_in_notebook(&input).await.map_err(|e| {
        tracing::error!(error = %e, "Failed to store revision entry");
        e
    })?;

    tracing::info!(
        revision_id = %revision_id,
        original_id = %entry_id,
        sequence = causal_position.sequence,
        "Entry revised successfully"
    );

    // Publish event to SSE subscribers
    let broadcaster = state.broadcaster();
    if let Some(subscriber_count) = broadcaster
        .publish_entry(
            *notebook_id.as_uuid(),
            *revision_id.as_uuid(),
            "revise",
            integration_cost,
            causal_position.sequence,
        )
        .await
    {
        tracing::debug!(
            revision_id = %revision_id,
            subscribers = subscriber_count,
            "Published revise event to SSE subscribers"
        );
    }

    // Build response with headers
    let mut headers = HeaderMap::new();
    headers.insert(
        "X-Integration-Cost-Computed",
        HeaderValue::from_static(if cost_computed { "true" } else { "false" }),
    );

    Ok((
        headers,
        Json(ReviseResponse {
            revision_id,
            causal_position,
            integration_cost,
        }),
    ))
}

/// GET /notebooks/:notebook_id/entries/:entry_id - Get an entry with metadata.
///
/// Returns the full entry along with revision history, references, and
/// entries that reference this one.
///
/// # Query Parameters
///
/// - `revision`: Optional revision number (0 = current entry, 1 = first revision, etc.)
///
/// # Response
///
/// - 200 OK: `{ "entry": {...}, "revisions": [...], "references": [...], "referenced_by": [...] }`
/// - 400 Bad Request: Invalid revision number
/// - 404 Not Found: Notebook or entry not found
async fn get_entry(
    State(state): State<AppState>,
    identity: AuthorIdentity,
    Path((notebook_id, entry_id)): Path<(Uuid, Uuid)>,
    Query(params): Query<GetEntryParams>,
) -> ApiResult<Json<ReadEntryResponse>> {
    require_scope(&identity, "notebook:read", state.config())?;
    // Create repository from store
    let repo = Repository::new(state.store().clone());

    // First verify the notebook exists
    let _notebook = repo
        .get_notebook(NotebookId::from_uuid(notebook_id))
        .await
        .map_err(|e| match e {
            StoreError::NotebookNotFound(_) => {
                ApiError::NotFound(format!("Notebook {} not found", notebook_id))
            }
            _ => ApiError::from(e),
        })?;

    let entry_id = EntryId::from_uuid(entry_id);

    // Get the entry (optionally at specific revision)
    let entry = match params.revision {
        Some(rev) if rev > 0 => {
            // Get specific revision
            repo.get_entry_revision(entry_id, rev).await.map_err(|e| {
                match e {
                    StoreError::EntryNotFound(_) => {
                        // Could be entry not found or revision out of bounds
                        ApiError::BadRequest(format!(
                            "Revision {} not found for entry {}",
                            rev, entry_id
                        ))
                    }
                    _ => ApiError::from(e),
                }
            })?
        }
        _ => {
            // Get current entry (revision 0 or not specified)
            repo.get_entry(entry_id).await.map_err(|e| match e {
                StoreError::EntryNotFound(_) => {
                    ApiError::NotFound(format!("Entry {} not found", entry_id))
                }
                _ => ApiError::from(e),
            })?
        }
    };

    // Get revision chain (entries that revise this entry)
    let revision_chain = repo.get_revision_chain(entry_id).await.unwrap_or_default();
    let revisions: Vec<EntrySummary> = revision_chain.iter().map(entry_to_summary).collect();

    // Get references (entries this entry references)
    let refs = repo.get_references(entry_id).await.unwrap_or_default();
    let references: Vec<EntrySummary> = refs.iter().map(entry_to_summary).collect();

    // Get referenced_by (entries that reference this entry)
    let citing = repo.get_referencing(entry_id).await.unwrap_or_default();
    let referenced_by: Vec<EntrySummary> = citing.iter().map(entry_to_summary).collect();

    tracing::debug!(
        entry_id = %entry_id,
        revisions_count = revisions.len(),
        references_count = references.len(),
        referenced_by_count = referenced_by.len(),
        "Entry retrieved"
    );

    Ok(Json(ReadEntryResponse {
        entry: entry_to_response(&entry),
        revisions,
        references,
        referenced_by,
    }))
}

/// Build entry routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/notebooks/{id}/entries", post(create_entry))
        .route(
            "/notebooks/{id}/entries/{entry_id}",
            put(revise_entry).get(get_entry),
        )
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    // ========================================================================
    // CreateEntry Tests
    // ========================================================================

    #[test]
    fn test_create_entry_request_deserialize_minimal() {
        let json = r#"{"content": "hello world", "content_type": "text/plain"}"#;
        let request: CreateEntryRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.content, "hello world");
        assert_eq!(request.content_type, "text/plain");
        assert!(request.topic.is_none());
        assert!(request.references.is_empty());
    }

    #[test]
    fn test_create_entry_request_deserialize_full() {
        let json = r#"{
            "content": "hello world",
            "content_type": "text/plain",
            "topic": "greeting",
            "references": ["550e8400-e29b-41d4-a716-446655440000"]
        }"#;
        let request: CreateEntryRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.content, "hello world");
        assert_eq!(request.content_type, "text/plain");
        assert_eq!(request.topic, Some("greeting".to_string()));
        assert_eq!(request.references.len(), 1);
    }

    #[test]
    fn test_create_entry_response_serialize() {
        let response = CreateEntryResponse {
            entry_id: Uuid::nil(),
            causal_position: CausalPosition::first(),
            integration_cost: IntegrationCost::zero(),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("entry_id"));
        assert!(json.contains("causal_position"));
        assert!(json.contains("integration_cost"));
    }

    #[test]
    fn test_is_binary_content_type() {
        // Text types should NOT be treated as binary
        assert!(!is_binary_content_type("text/plain"));
        assert!(!is_binary_content_type("text/html"));
        assert!(!is_binary_content_type("application/json"));
        assert!(!is_binary_content_type("application/xml"));
        assert!(!is_binary_content_type("application/javascript"));

        // Binary types SHOULD be treated as binary
        assert!(is_binary_content_type("application/octet-stream"));
        assert!(is_binary_content_type("image/png"));
        assert!(is_binary_content_type("application/pdf"));
    }

    #[test]
    fn test_get_content_bytes_text() {
        let request = CreateEntryRequest {
            content: "hello world".to_string(),
            content_type: "text/plain".to_string(),
            topic: None,
            references: vec![],
        };
        let bytes = get_content_bytes(&request).unwrap();
        assert_eq!(bytes, b"hello world");
    }

    #[test]
    fn test_get_content_bytes_json() {
        let request = CreateEntryRequest {
            content: r#"{"key": "value"}"#.to_string(),
            content_type: "application/json".to_string(),
            topic: None,
            references: vec![],
        };
        let bytes = get_content_bytes(&request).unwrap();
        assert_eq!(bytes, br#"{"key": "value"}"#);
    }

    #[test]
    fn test_get_content_bytes_binary_base64() {
        use base64::{Engine, engine::general_purpose::STANDARD as BASE64};
        let original = b"binary data here";
        let encoded = BASE64.encode(original);

        let request = CreateEntryRequest {
            content: encoded,
            content_type: "application/octet-stream".to_string(),
            topic: None,
            references: vec![],
        };
        let bytes = get_content_bytes(&request).unwrap();
        assert_eq!(bytes, original);
    }

    #[test]
    fn test_get_content_bytes_invalid_base64() {
        let request = CreateEntryRequest {
            content: "not valid base64!!!".to_string(),
            content_type: "application/octet-stream".to_string(),
            topic: None,
            references: vec![],
        };
        let result = get_content_bytes(&request);
        assert!(result.is_err());
    }

    // ========================================================================
    // ReviseEntry Tests
    // ========================================================================

    #[test]
    fn test_revise_request_deserialize() {
        let json = r#"{"content": "new content"}"#;
        let request: ReviseRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.content, "new content");
        assert!(request.reason.is_none());
    }

    #[test]
    fn test_revise_request_with_reason() {
        let json = r#"{"content": "new content", "reason": "fixing typo"}"#;
        let request: ReviseRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.content, "new content");
        assert_eq!(request.reason, Some("fixing typo".to_string()));
    }

    #[test]
    fn test_revise_response_serialize() {
        let response = ReviseResponse {
            revision_id: EntryId::from_uuid(Uuid::nil()),
            causal_position: CausalPosition::first(),
            integration_cost: IntegrationCost::zero(),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("revision_id"));
        assert!(json.contains("causal_position"));
        assert!(json.contains("integration_cost"));
    }

    // ========================================================================
    // ReadEntry Tests
    // ========================================================================

    #[test]
    fn test_encode_content_text_plain() {
        let content = b"Hello, world!";
        let result = encode_content(content, "text/plain");
        match result {
            EntryContent::Text(s) => assert_eq!(s, "Hello, world!"),
            _ => panic!("Expected Text variant"),
        }
    }

    #[test]
    fn test_encode_content_text_markdown() {
        let content = b"# Header\n\nParagraph";
        let result = encode_content(content, "text/markdown");
        match result {
            EntryContent::Text(s) => assert_eq!(s, "# Header\n\nParagraph"),
            _ => panic!("Expected Text variant"),
        }
    }

    #[test]
    fn test_encode_content_binary() {
        let content = b"\x00\x01\x02\x03";
        let result = encode_content(content, "application/octet-stream");
        match result {
            EntryContent::Binary { data, encoding } => {
                assert_eq!(encoding, "base64");
                assert_eq!(data, "AAECAw==");
            }
            _ => panic!("Expected Binary variant"),
        }
    }

    #[test]
    fn test_encode_content_json() {
        let content = b"{\"key\": \"value\"}";
        let result = encode_content(content, "application/json");
        match result {
            EntryContent::Binary { data, encoding } => {
                assert_eq!(encoding, "base64");
                // JSON is not text/*, so it gets base64 encoded
                let decoded = base64::engine::general_purpose::STANDARD
                    .decode(&data)
                    .unwrap();
                assert_eq!(decoded, b"{\"key\": \"value\"}");
            }
            _ => panic!("Expected Binary variant"),
        }
    }

    #[test]
    fn test_encode_content_invalid_utf8_text() {
        // Invalid UTF-8 sequence
        let content = b"\xff\xfe";
        let result = encode_content(content, "text/plain");
        // Should fall back to base64 since it's not valid UTF-8
        match result {
            EntryContent::Binary { encoding, .. } => {
                assert_eq!(encoding, "base64");
            }
            _ => panic!("Expected Binary variant for invalid UTF-8"),
        }
    }

    #[test]
    fn test_encode_content_empty() {
        let content = b"";
        let result = encode_content(content, "text/plain");
        match result {
            EntryContent::Text(s) => assert_eq!(s, ""),
            _ => panic!("Expected Text variant"),
        }
    }

    #[test]
    fn test_get_entry_params_deserialize_none() {
        let params: GetEntryParams = serde_urlencoded::from_str("").unwrap();
        assert!(params.revision.is_none());
    }

    #[test]
    fn test_get_entry_params_deserialize_revision() {
        let params: GetEntryParams = serde_urlencoded::from_str("revision=2").unwrap();
        assert_eq!(params.revision, Some(2));
    }

    #[test]
    fn test_read_entry_response_serialize() {
        let author = AuthorId::zero();
        let response = ReadEntryResponse {
            entry: EntryResponse {
                id: EntryId::from_uuid(Uuid::nil()),
                content: EntryContent::Text("test".to_string()),
                content_type: "text/plain".to_string(),
                topic: Some("test-topic".to_string()),
                author,
                references: vec![],
                revision_of: None,
                causal_position: CausalPositionResponse {
                    sequence: 1,
                    activity_context: ActivityContextResponse {
                        entries_since_last_by_author: 0,
                        total_notebook_entries: 1,
                        recent_entropy: 0.0,
                    },
                },
                created: Utc::now(),
                integration_cost: IntegrationCost::zero(),
            },
            revisions: vec![],
            references: vec![],
            referenced_by: vec![],
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("entry"));
        assert!(json.contains("revisions"));
        assert!(json.contains("references"));
        assert!(json.contains("referenced_by"));
    }

    #[test]
    fn test_entry_summary_serialize() {
        let summary = EntrySummary {
            id: EntryId::from_uuid(Uuid::nil()),
            topic: Some("test".to_string()),
            author: AuthorId::zero(),
            created: Utc::now(),
        };
        let json = serde_json::to_string(&summary).unwrap();
        assert!(json.contains("id"));
        assert!(json.contains("topic"));
        assert!(json.contains("author"));
        assert!(json.contains("created"));
    }
}
