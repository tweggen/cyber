//! Author registration endpoints.
//!
//! These endpoints allow the ASP.NET Core shell to register authors
//! in the notebook system.
//!
//! - POST /authors — register a new author with public key
//! - GET /authors/{id} — get author by AuthorId (hex)

use axum::{
    Json, Router,
    extract::{Path, State},
    http::StatusCode,
    routing::{get, post},
};
use serde::{Deserialize, Serialize};

use notebook_store::NewAuthor;

use crate::error::{ApiError, ApiResult};
use crate::state::AppState;

/// Request body for registering a new author.
#[derive(Debug, Deserialize)]
pub struct RegisterAuthorRequest {
    /// Ed25519 public key as 64-character hex string.
    pub public_key: String,
}

/// Response for author registration.
#[derive(Debug, Serialize)]
pub struct RegisterAuthorResponse {
    /// The computed AuthorId (BLAKE3 hash of public key) as hex.
    pub author_id: String,
}

/// Response for getting an author.
#[derive(Debug, Serialize)]
pub struct AuthorResponse {
    /// AuthorId as hex.
    pub author_id: String,
    /// Public key as hex.
    pub public_key: String,
    /// Creation timestamp.
    pub created: String,
}

/// POST /authors — Register a new author.
async fn register_author(
    State(state): State<AppState>,
    Json(request): Json<RegisterAuthorRequest>,
) -> ApiResult<(StatusCode, Json<RegisterAuthorResponse>)> {
    // Parse public key
    if request.public_key.len() != 64 {
        return Err(ApiError::BadRequest(format!(
            "public_key must be 64 hex characters, got {}",
            request.public_key.len()
        )));
    }

    let pk_bytes = hex::decode(&request.public_key)
        .map_err(|e| ApiError::BadRequest(format!("Invalid hex in public_key: {}", e)))?;

    let mut public_key = [0u8; 32];
    public_key.copy_from_slice(&pk_bytes);

    // Compute AuthorId as BLAKE3 hash of public key
    let author_id_hash = blake3::hash(&public_key);
    let author_id: [u8; 32] = *author_id_hash.as_bytes();

    // Insert author
    let new_author = NewAuthor::new(author_id, public_key);
    state
        .store()
        .insert_author(&new_author)
        .await
        .map_err(|e| {
            tracing::warn!(error = %e, "Failed to register author");
            ApiError::Store(e)
        })?;

    let author_id_hex = hex::encode(author_id);
    tracing::info!(author_id = %author_id_hex, "Author registered");

    Ok((
        StatusCode::CREATED,
        Json(RegisterAuthorResponse {
            author_id: author_id_hex,
        }),
    ))
}

/// GET /authors/{id} — Get author by AuthorId.
async fn get_author(
    State(state): State<AppState>,
    Path(author_id_hex): Path<String>,
) -> ApiResult<Json<AuthorResponse>> {
    if author_id_hex.len() != 64 {
        return Err(ApiError::BadRequest(format!(
            "author_id must be 64 hex characters, got {}",
            author_id_hex.len()
        )));
    }

    let id_bytes = hex::decode(&author_id_hex)
        .map_err(|e| ApiError::BadRequest(format!("Invalid hex in author_id: {}", e)))?;

    let mut author_id = [0u8; 32];
    author_id.copy_from_slice(&id_bytes);

    let row =
        state.store().get_author(&author_id).await.map_err(|e| {
            ApiError::NotFound(format!("Author {} not found: {}", author_id_hex, e))
        })?;

    Ok(Json(AuthorResponse {
        author_id: hex::encode(&row.id),
        public_key: hex::encode(&row.public_key),
        created: row.created.to_rfc3339(),
    }))
}

/// Build author routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/authors", post(register_author))
        .route("/authors/{id}", get(get_author))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_register_request_deserialize() {
        let json =
            r#"{"public_key": "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20"}"#;
        let request: RegisterAuthorRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.public_key.len(), 64);
    }

    #[test]
    fn test_register_response_serialize() {
        let response = RegisterAuthorResponse {
            author_id: "00".repeat(32),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("author_id"));
    }
}
