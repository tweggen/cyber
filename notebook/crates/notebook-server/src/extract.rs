//! Author identity extraction from X-Author-Id header.

use axum::{extract::FromRequestParts, http::request::Parts};
use notebook_core::AuthorId;

use crate::error::ApiError;
use crate::state::AppState;

/// Extracts AuthorId from the `X-Author-Id` header.
///
/// If the header is missing, returns `AuthorId::zero()` (backward compat / dev mode).
/// If the header is present but malformed, returns `ApiError::BadRequest`.
pub struct AuthorIdentity(pub AuthorId);

impl FromRequestParts<AppState> for AuthorIdentity {
    type Rejection = ApiError;

    async fn from_request_parts(
        parts: &mut Parts,
        _state: &AppState,
    ) -> Result<Self, Self::Rejection> {
        let Some(header_value) = parts.headers.get("X-Author-Id") else {
            tracing::warn!("X-Author-Id header missing, using zero author");
            return Ok(AuthorIdentity(AuthorId::zero()));
        };

        let hex_str = header_value.to_str().map_err(|_| {
            ApiError::BadRequest("X-Author-Id header contains invalid characters".to_string())
        })?;

        if hex_str.len() != 64 {
            return Err(ApiError::BadRequest(format!(
                "X-Author-Id must be 64 hex characters, got {}",
                hex_str.len()
            )));
        }

        let bytes = hex::decode(hex_str)
            .map_err(|e| ApiError::BadRequest(format!("Invalid hex in X-Author-Id: {}", e)))?;

        let mut arr = [0u8; 32];
        arr.copy_from_slice(&bytes);
        Ok(AuthorIdentity(AuthorId::from_bytes(arr)))
    }
}
