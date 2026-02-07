//! Notebook discovery and management routes for the Knowledge Exchange Platform.
//!
//! This module implements the notebook-related HTTP endpoints:
//! - GET /notebooks - List accessible notebooks with stats
//! - POST /notebooks - Create a new notebook
//! - DELETE /notebooks/{id} - Delete a notebook (owner only)
//!
//! Owned by: agent-discovery

use axum::{
    Json, Router,
    extract::{Path, State},
    http::StatusCode,
    routing::{delete, get},
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_core::Permissions;
use notebook_store::{NewNotebook, Store, StoreError};

use crate::error::{ApiError, ApiResult};
use crate::extract::AuthorIdentity;
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

/// Summary of a notebook in the list response.
#[derive(Debug, Serialize)]
pub struct NotebookSummary {
    /// Notebook ID.
    pub id: Uuid,
    /// Notebook name.
    pub name: String,
    /// Owner author ID (hex encoded).
    pub owner: String,
    /// Whether the current user is the owner.
    pub is_owner: bool,
    /// Permissions for the current user.
    pub permissions: NotebookPermissions,
    /// Total number of entries in the notebook.
    pub total_entries: i64,
    /// Total entropy (sum of catalog_shift values).
    pub total_entropy: f64,
    /// Sequence number of the most recent entry.
    pub last_activity_sequence: i64,
    /// Number of participants with access.
    pub participant_count: i64,
}

/// Permissions for a notebook.
#[derive(Debug, Serialize)]
pub struct NotebookPermissions {
    pub read: bool,
    pub write: bool,
}

impl From<Permissions> for NotebookPermissions {
    fn from(p: Permissions) -> Self {
        Self {
            read: p.read,
            write: p.write,
        }
    }
}

/// Response for GET /notebooks.
#[derive(Debug, Serialize)]
pub struct ListNotebooksResponse {
    pub notebooks: Vec<NotebookSummary>,
}

/// Request body for POST /notebooks.
#[derive(Debug, Deserialize)]
pub struct CreateNotebookRequest {
    /// Name for the new notebook.
    pub name: String,
}

/// Response for POST /notebooks.
#[derive(Debug, Serialize)]
pub struct CreateNotebookResponse {
    /// The created notebook's ID.
    pub id: Uuid,
    /// The notebook name.
    pub name: String,
    /// Owner author ID (hex encoded).
    pub owner: String,
    /// Creation timestamp.
    pub created: DateTime<Utc>,
}

/// Response for DELETE /notebooks/{id}.
#[derive(Debug, Serialize)]
pub struct DeleteNotebookResponse {
    /// ID of the deleted notebook.
    pub id: Uuid,
    /// Confirmation message.
    pub message: String,
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Convert a 32-byte author ID to hex string.
fn author_id_to_hex(id: &[u8]) -> String {
    id.iter().map(|b| format!("{:02x}", b)).collect()
}

/// Get notebook statistics including entropy and last activity.
async fn get_notebook_extended_stats(
    store: &Store,
    notebook_id: Uuid,
) -> StoreResult<(f64, i64, i64)> {
    // Query for total_entropy and last_activity_sequence
    let stats: (Option<f64>, Option<i64>, i64) = sqlx::query_as(
        r#"
        SELECT
            SUM((integration_cost->>'catalog_shift')::float8) as total_entropy,
            MAX(sequence) as last_activity_sequence,
            COUNT(*)::bigint as entry_count
        FROM entries
        WHERE notebook_id = $1
        "#,
    )
    .bind(notebook_id)
    .fetch_one(store.pool())
    .await?;

    Ok((stats.0.unwrap_or(0.0), stats.1.unwrap_or(0), stats.2))
}

use notebook_store::StoreResult;

/// Get participant count for a notebook.
async fn get_participant_count(store: &Store, notebook_id: Uuid) -> StoreResult<i64> {
    let count: (i64,) = sqlx::query_as(
        r#"
        SELECT COUNT(*)::bigint
        FROM notebook_access
        WHERE notebook_id = $1
        "#,
    )
    .bind(notebook_id)
    .fetch_one(store.pool())
    .await?;

    Ok(count.0)
}

/// Get permissions for an author on a notebook.
async fn get_author_permissions(
    store: &Store,
    notebook_id: Uuid,
    author_id: &[u8; 32],
) -> StoreResult<(bool, bool)> {
    let result: Option<(bool, bool)> = sqlx::query_as(
        r#"
        SELECT read, write
        FROM notebook_access
        WHERE notebook_id = $1 AND author_id = $2
        "#,
    )
    .bind(notebook_id)
    .bind(author_id.as_slice())
    .fetch_optional(store.pool())
    .await?;

    Ok(result.unwrap_or((false, false)))
}

// ============================================================================
// Route Handlers
// ============================================================================

/// GET /notebooks - List accessible notebooks with stats.
///
/// Returns all notebooks the authenticated user has access to, including
/// ownership status, permissions, and statistics.
///
/// # Response
///
/// - 200 OK: `{ "notebooks": [...] }`
/// - 401 Unauthorized: No authentication (future)
async fn list_notebooks(
    State(state): State<AppState>,
    AuthorIdentity(author_id): AuthorIdentity,
) -> ApiResult<Json<ListNotebooksResponse>> {
    let store = state.store();

    let author_bytes = *author_id.as_bytes();

    // List notebooks accessible to this author
    let notebook_rows = store.list_notebooks_for_author(&author_bytes).await?;

    let mut notebooks = Vec::with_capacity(notebook_rows.len());

    for row in notebook_rows {
        // Get extended stats (entropy, last activity, entry count)
        let (total_entropy, last_activity_sequence, total_entries) =
            get_notebook_extended_stats(store, row.id)
                .await
                .unwrap_or((0.0, 0, 0));

        // Get participant count
        let participant_count = get_participant_count(store, row.id).await.unwrap_or(0);

        // Get permissions for this author
        let (read, write) = get_author_permissions(store, row.id, &author_bytes)
            .await
            .unwrap_or((false, false));

        // Check if this author is the owner
        let owner_bytes: [u8; 32] = row.owner_id.as_slice().try_into().unwrap_or([0u8; 32]);
        let is_owner = owner_bytes == author_bytes;

        notebooks.push(NotebookSummary {
            id: row.id,
            name: row.name,
            owner: author_id_to_hex(&row.owner_id),
            is_owner,
            permissions: NotebookPermissions { read, write },
            total_entries,
            total_entropy,
            last_activity_sequence,
            participant_count,
        });
    }

    // Sort by last_activity_sequence descending (most recent first)
    notebooks.sort_by(|a, b| b.last_activity_sequence.cmp(&a.last_activity_sequence));

    tracing::info!(count = notebooks.len(), "Listed notebooks for author");

    Ok(Json(ListNotebooksResponse { notebooks }))
}

/// POST /notebooks - Create a new notebook.
///
/// Creates a new notebook owned by the authenticated user.
///
/// # Request
///
/// Body: `{ "name": "My Notebook" }`
///
/// # Response
///
/// - 201 Created: `{ "id": "...", "name": "...", "owner": "...", "created": "..." }`
/// - 400 Bad Request: Invalid request body
/// - 401 Unauthorized: No authentication (future)
async fn create_notebook(
    State(state): State<AppState>,
    AuthorIdentity(author_id): AuthorIdentity,
    Json(request): Json<CreateNotebookRequest>,
) -> ApiResult<(StatusCode, Json<CreateNotebookResponse>)> {
    let store = state.store();

    let author_bytes = *author_id.as_bytes();

    // Validate name is not empty
    if request.name.trim().is_empty() {
        return Err(ApiError::BadRequest(
            "Notebook name cannot be empty".to_string(),
        ));
    }

    // Create the notebook
    let new_notebook = NewNotebook::new(request.name.clone(), author_bytes);
    let notebook_row = store.insert_notebook(&new_notebook).await.map_err(|e| {
        tracing::error!(error = %e, "Failed to create notebook");
        ApiError::Store(e)
    })?;

    tracing::info!(
        notebook_id = %notebook_row.id,
        name = %notebook_row.name,
        "Notebook created"
    );

    Ok((
        StatusCode::CREATED,
        Json(CreateNotebookResponse {
            id: notebook_row.id,
            name: notebook_row.name,
            owner: author_id_to_hex(&notebook_row.owner_id),
            created: notebook_row.created,
        }),
    ))
}

/// DELETE /notebooks/{id} - Delete a notebook.
///
/// Deletes a notebook. Only the owner can delete a notebook.
/// Currently performs a hard delete (soft delete requires schema migration).
///
/// # Response
///
/// - 200 OK: `{ "id": "...", "message": "Notebook deleted" }`
/// - 401 Unauthorized: No authentication (future)
/// - 403 Forbidden: Not the owner
/// - 404 Not Found: Notebook doesn't exist
async fn delete_notebook(
    State(state): State<AppState>,
    AuthorIdentity(author_id): AuthorIdentity,
    Path(notebook_id): Path<Uuid>,
) -> ApiResult<Json<DeleteNotebookResponse>> {
    let store = state.store();

    let author_bytes = *author_id.as_bytes();

    // Get the notebook to check ownership
    let notebook_row = store.get_notebook(notebook_id).await.map_err(|e| match e {
        StoreError::NotebookNotFound(id) => {
            ApiError::NotFound(format!("Notebook {} not found", id))
        }
        other => ApiError::Store(other),
    })?;

    // Check ownership
    let owner_bytes: [u8; 32] = notebook_row
        .owner_id
        .as_slice()
        .try_into()
        .map_err(|_| ApiError::Internal("Invalid owner_id in database".to_string()))?;

    if owner_bytes != author_bytes {
        return Err(ApiError::Forbidden(
            "Only the notebook owner can delete it".to_string(),
        ));
    }

    // Delete entries first (foreign key constraint)
    sqlx::query("DELETE FROM entries WHERE notebook_id = $1")
        .bind(notebook_id)
        .execute(store.pool())
        .await
        .map_err(|e| {
            tracing::error!(error = %e, "Failed to delete notebook entries");
            ApiError::Internal("Failed to delete notebook entries".to_string())
        })?;

    // Delete access records
    sqlx::query("DELETE FROM notebook_access WHERE notebook_id = $1")
        .bind(notebook_id)
        .execute(store.pool())
        .await
        .map_err(|e| {
            tracing::error!(error = %e, "Failed to delete notebook access");
            ApiError::Internal("Failed to delete notebook access".to_string())
        })?;

    // Delete the notebook
    sqlx::query("DELETE FROM notebooks WHERE id = $1")
        .bind(notebook_id)
        .execute(store.pool())
        .await
        .map_err(|e| {
            tracing::error!(error = %e, "Failed to delete notebook");
            ApiError::Internal("Failed to delete notebook".to_string())
        })?;

    tracing::info!(
        notebook_id = %notebook_id,
        "Notebook deleted"
    );

    Ok(Json(DeleteNotebookResponse {
        id: notebook_id,
        message: "Notebook deleted successfully".to_string(),
    }))
}

/// Build notebook routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/notebooks", get(list_notebooks).post(create_notebook))
        .route("/notebooks/{id}", delete(delete_notebook))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_author_id_to_hex() {
        let bytes = [0u8; 32];
        let hex = author_id_to_hex(&bytes);
        assert_eq!(hex.len(), 64);
        assert!(hex.chars().all(|c| c == '0'));
    }

    #[test]
    fn test_author_id_to_hex_nonzero() {
        let bytes = [0xff; 32];
        let hex = author_id_to_hex(&bytes);
        assert_eq!(hex.len(), 64);
        assert!(hex.chars().all(|c| c == 'f'));
    }

    #[test]
    fn test_create_notebook_request_deserialize() {
        let json = r#"{"name": "My Notebook"}"#;
        let request: CreateNotebookRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.name, "My Notebook");
    }

    #[test]
    fn test_notebook_summary_serialize() {
        let summary = NotebookSummary {
            id: Uuid::nil(),
            name: "Test Notebook".to_string(),
            owner: "00".repeat(32),
            is_owner: true,
            permissions: NotebookPermissions {
                read: true,
                write: true,
            },
            total_entries: 10,
            total_entropy: 5.5,
            last_activity_sequence: 100,
            participant_count: 3,
        };
        let json = serde_json::to_string(&summary).unwrap();
        assert!(json.contains("Test Notebook"));
        assert!(json.contains("total_entries"));
        assert!(json.contains("total_entropy"));
        assert!(json.contains("last_activity_sequence"));
        assert!(json.contains("participant_count"));
    }

    #[test]
    fn test_permissions_from_core() {
        let core_perms = Permissions {
            read: true,
            write: false,
        };
        let api_perms: NotebookPermissions = core_perms.into();
        assert!(api_perms.read);
        assert!(!api_perms.write);
    }

    #[test]
    fn test_delete_response_serialize() {
        let response = DeleteNotebookResponse {
            id: Uuid::nil(),
            message: "Deleted".to_string(),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("message"));
        assert!(json.contains("Deleted"));
    }
}
