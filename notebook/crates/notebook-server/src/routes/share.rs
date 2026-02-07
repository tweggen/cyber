//! Share routes for the Knowledge Exchange Platform.
//!
//! This module implements the sharing-related HTTP endpoints:
//! - POST /notebooks/{id}/share - Grant access to a notebook
//! - DELETE /notebooks/{id}/share/{author_id} - Revoke access
//! - GET /notebooks/{id}/participants - List participants
//!
//! Owned by: agent-share

use axum::{
    Json, Router,
    extract::{Path, State},
    routing::{delete, get, post},
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_store::NewNotebookAccess;

use crate::error::{ApiError, ApiResult};
use crate::extract::AuthorIdentity;
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

/// Permissions for notebook access.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Permissions {
    /// Whether the author can read entries.
    pub read: bool,
    /// Whether the author can write entries.
    pub write: bool,
}

/// Request body for granting access to a notebook.
#[derive(Debug, Deserialize)]
pub struct ShareRequest {
    /// The author ID to grant access to (64-character hex string).
    pub author_id: String,
    /// The permissions to grant.
    pub permissions: Permissions,
}

/// Response for successful access grant.
#[derive(Debug, Serialize)]
pub struct ShareResponse {
    /// Whether access was granted.
    pub access_granted: bool,
    /// The author ID that was granted access.
    pub author_id: String,
    /// The permissions that were granted.
    pub permissions: Permissions,
}

/// Response for successful access revocation.
#[derive(Debug, Serialize)]
pub struct RevokeResponse {
    /// Whether access was revoked.
    pub access_revoked: bool,
    /// The author ID whose access was revoked.
    pub author_id: String,
}

/// A participant in a notebook.
#[derive(Debug, Serialize)]
pub struct Participant {
    /// The author ID (64-character hex string).
    pub author_id: String,
    /// The permissions granted.
    pub permissions: Permissions,
    /// When access was granted.
    pub granted_at: DateTime<Utc>,
}

/// Response for listing participants.
#[derive(Debug, Serialize)]
pub struct ParticipantsResponse {
    /// List of participants with access to the notebook.
    pub participants: Vec<Participant>,
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Parse a 64-character hex string into a 32-byte array.
fn parse_author_id(hex_str: &str) -> Result<[u8; 32], ApiError> {
    if hex_str.len() != 64 {
        return Err(ApiError::BadRequest(format!(
            "author_id must be 64 hex characters, got {}",
            hex_str.len()
        )));
    }

    let bytes = hex::decode(hex_str)
        .map_err(|e| ApiError::BadRequest(format!("Invalid hex in author_id: {}", e)))?;

    let mut arr = [0u8; 32];
    arr.copy_from_slice(&bytes);
    Ok(arr)
}

/// Convert a 32-byte array to a 64-character hex string.
fn author_id_to_hex(bytes: &[u8]) -> String {
    hex::encode(bytes)
}

/// Check if an author is the owner of a notebook.
async fn is_notebook_owner(
    state: &AppState,
    notebook_id: Uuid,
    author_id: &[u8; 32],
) -> ApiResult<bool> {
    let notebook = state
        .store()
        .get_notebook(notebook_id)
        .await
        .map_err(|e| match e {
            notebook_store::StoreError::NotebookNotFound(_) => {
                ApiError::NotFound(format!("Notebook {} not found", notebook_id))
            }
            other => ApiError::Store(other),
        })?;

    Ok(notebook.owner_id == author_id.as_slice())
}

// ============================================================================
// Route Handlers
// ============================================================================

/// POST /notebooks/:id/share - Grant access to a notebook.
///
/// Grants read and/or write access to the specified author.
/// Only the notebook owner can grant access.
///
/// # Request
///
/// Body: `{ "author_id": "hex_string", "permissions": { "read": true, "write": false } }`
/// Header: `X-Author-Id` - The requesting author (must be owner)
///
/// # Response
///
/// - 200 OK: `{ "access_granted": true, "author_id": "...", "permissions": {...} }`
/// - 400 Bad Request: Invalid author_id format
/// - 403 Forbidden: Requester is not the owner
/// - 404 Not Found: Notebook not found
async fn grant_access(
    State(state): State<AppState>,
    AuthorIdentity(author_identity): AuthorIdentity,
    Path(notebook_id): Path<Uuid>,
    Json(request): Json<ShareRequest>,
) -> ApiResult<Json<ShareResponse>> {
    // Parse target author_id
    let target_author_id = parse_author_id(&request.author_id)?;

    let requester_id = *author_identity.as_bytes();

    // Check if requester is owner
    if !is_notebook_owner(&state, notebook_id, &requester_id).await? {
        return Err(ApiError::Forbidden(
            "Only the notebook owner can grant access".to_string(),
        ));
    }

    // Grant access using store
    let access = NewNotebookAccess {
        notebook_id,
        author_id: target_author_id,
        read: request.permissions.read,
        write: request.permissions.write,
    };

    state.store().grant_access(&access).await?;

    tracing::info!(
        notebook_id = %notebook_id,
        target_author = %request.author_id,
        read = request.permissions.read,
        write = request.permissions.write,
        "Access granted"
    );

    Ok(Json(ShareResponse {
        access_granted: true,
        author_id: request.author_id,
        permissions: request.permissions,
    }))
}

/// DELETE /notebooks/:id/share/:author_id - Revoke access from a notebook.
///
/// Removes access for the specified author.
/// Only the notebook owner can revoke access.
/// Cannot revoke the owner's own access.
///
/// # Response
///
/// - 200 OK: `{ "access_revoked": true, "author_id": "..." }`
/// - 400 Bad Request: Invalid author_id format or trying to revoke owner
/// - 403 Forbidden: Requester is not the owner
/// - 404 Not Found: Notebook not found
async fn revoke_access(
    State(state): State<AppState>,
    AuthorIdentity(author_identity): AuthorIdentity,
    Path((notebook_id, author_id_hex)): Path<(Uuid, String)>,
) -> ApiResult<Json<RevokeResponse>> {
    // Parse target author_id
    let target_author_id = parse_author_id(&author_id_hex)?;

    let requester_id = *author_identity.as_bytes();

    // Check if requester is owner
    if !is_notebook_owner(&state, notebook_id, &requester_id).await? {
        return Err(ApiError::Forbidden(
            "Only the notebook owner can revoke access".to_string(),
        ));
    }

    // Get notebook to check if target is owner
    let notebook = state.store().get_notebook(notebook_id).await?;
    if notebook.owner_id == target_author_id.as_slice() {
        return Err(ApiError::BadRequest(
            "Cannot revoke owner's access".to_string(),
        ));
    }

    // Revoke access using direct SQL (store doesn't have revoke_access method)
    let result =
        sqlx::query("DELETE FROM notebook_access WHERE notebook_id = $1 AND author_id = $2")
            .bind(notebook_id)
            .bind(target_author_id.as_slice())
            .execute(state.store().pool())
            .await
            .map_err(|e| ApiError::Internal(format!("Failed to revoke access: {}", e)))?;

    if result.rows_affected() == 0 {
        return Err(ApiError::NotFound(format!(
            "No access found for author {} on notebook {}",
            author_id_hex, notebook_id
        )));
    }

    tracing::info!(
        notebook_id = %notebook_id,
        target_author = %author_id_hex,
        "Access revoked"
    );

    Ok(Json(RevokeResponse {
        access_revoked: true,
        author_id: author_id_hex,
    }))
}

/// GET /notebooks/:id/participants - List participants with access.
///
/// Returns all authors with access to the notebook.
/// Any participant with read access can view the list.
///
/// # Response
///
/// - 200 OK: `{ "participants": [{ "author_id": "...", "permissions": {...}, "granted_at": "..." }] }`
/// - 403 Forbidden: Requester has no access
/// - 404 Not Found: Notebook not found
async fn list_participants(
    State(state): State<AppState>,
    AuthorIdentity(author_identity): AuthorIdentity,
    Path(notebook_id): Path<Uuid>,
) -> ApiResult<Json<ParticipantsResponse>> {
    // Verify notebook exists
    let _ = state
        .store()
        .get_notebook(notebook_id)
        .await
        .map_err(|e| match e {
            notebook_store::StoreError::NotebookNotFound(_) => {
                ApiError::NotFound(format!("Notebook {} not found", notebook_id))
            }
            other => ApiError::Store(other),
        })?;

    let requester_id = *author_identity.as_bytes();

    // Check if requester has read access
    if !state
        .store()
        .has_read_access(notebook_id, &requester_id)
        .await?
    {
        return Err(ApiError::Forbidden(
            "You do not have access to this notebook".to_string(),
        ));
    }

    // List all access records
    let access_list = state.store().list_notebook_access(notebook_id).await?;

    let participants: Vec<Participant> = access_list
        .into_iter()
        .map(|access| Participant {
            author_id: author_id_to_hex(&access.author_id),
            permissions: Permissions {
                read: access.read,
                write: access.write,
            },
            granted_at: access.granted,
        })
        .collect();

    tracing::debug!(
        notebook_id = %notebook_id,
        participant_count = participants.len(),
        "Listed participants"
    );

    Ok(Json(ParticipantsResponse { participants }))
}

/// Build share routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/notebooks/{id}/share", post(grant_access))
        .route("/notebooks/{id}/share/{author_id}", delete(revoke_access))
        .route("/notebooks/{id}/participants", get(list_participants))
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_author_id_valid() {
        let hex = "0000000000000000000000000000000000000000000000000000000000000000";
        let result = parse_author_id(hex).unwrap();
        assert_eq!(result, [0u8; 32]);
    }

    #[test]
    fn test_parse_author_id_valid_nonzero() {
        let hex = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";
        let result = parse_author_id(hex).unwrap();
        let expected: [u8; 32] = [
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32,
        ];
        assert_eq!(result, expected);
    }

    #[test]
    fn test_parse_author_id_wrong_length() {
        let hex = "0000"; // Too short
        let result = parse_author_id(hex);
        assert!(result.is_err());
        assert!(
            result
                .unwrap_err()
                .to_string()
                .contains("64 hex characters")
        );
    }

    #[test]
    fn test_parse_author_id_invalid_hex() {
        let hex = "gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg";
        let result = parse_author_id(hex);
        assert!(result.is_err());
        assert!(result.unwrap_err().to_string().contains("Invalid hex"));
    }

    #[test]
    fn test_author_id_to_hex() {
        let bytes = [0u8; 32];
        let hex = author_id_to_hex(&bytes);
        assert_eq!(
            hex,
            "0000000000000000000000000000000000000000000000000000000000000000"
        );
    }

    #[test]
    fn test_author_id_to_hex_nonzero() {
        let bytes: [u8; 32] = [
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32,
        ];
        let hex = author_id_to_hex(&bytes);
        assert_eq!(
            hex,
            "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20"
        );
    }

    #[test]
    fn test_share_request_deserialize() {
        let json = r#"{
            "author_id": "0000000000000000000000000000000000000000000000000000000000000001",
            "permissions": { "read": true, "write": false }
        }"#;
        let request: ShareRequest = serde_json::from_str(json).unwrap();
        assert_eq!(
            request.author_id,
            "0000000000000000000000000000000000000000000000000000000000000001"
        );
        assert!(request.permissions.read);
        assert!(!request.permissions.write);
    }

    #[test]
    fn test_share_response_serialize() {
        let response = ShareResponse {
            access_granted: true,
            author_id: "0000000000000000000000000000000000000000000000000000000000000001"
                .to_string(),
            permissions: Permissions {
                read: true,
                write: true,
            },
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("access_granted"));
        assert!(json.contains("author_id"));
        assert!(json.contains("permissions"));
    }

    #[test]
    fn test_revoke_response_serialize() {
        let response = RevokeResponse {
            access_revoked: true,
            author_id: "0000000000000000000000000000000000000000000000000000000000000001"
                .to_string(),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("access_revoked"));
        assert!(json.contains("author_id"));
    }

    #[test]
    fn test_participant_serialize() {
        let participant = Participant {
            author_id: "0000000000000000000000000000000000000000000000000000000000000001"
                .to_string(),
            permissions: Permissions {
                read: true,
                write: false,
            },
            granted_at: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&participant).unwrap();
        assert!(json.contains("author_id"));
        assert!(json.contains("permissions"));
        assert!(json.contains("granted_at"));
    }

    #[test]
    fn test_participants_response_serialize() {
        let response = ParticipantsResponse {
            participants: vec![Participant {
                author_id: "0000000000000000000000000000000000000000000000000000000000000001"
                    .to_string(),
                permissions: Permissions {
                    read: true,
                    write: true,
                },
                granted_at: chrono::Utc::now(),
            }],
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("participants"));
    }
}
