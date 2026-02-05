//! API error types with JSON responses.

use axum::{
    http::StatusCode,
    response::{IntoResponse, Response},
    Json,
};
use serde::Serialize;

/// API error that can be returned from handlers.
#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    /// Not implemented (501).
    #[error("not implemented: {0}")]
    NotImplemented(String),

    /// Bad request (400).
    #[error("bad request: {0}")]
    BadRequest(String),

    /// Not found (404).
    #[error("not found: {0}")]
    NotFound(String),

    /// Unauthorized (401).
    #[error("unauthorized: {0}")]
    Unauthorized(String),

    /// Forbidden (403).
    #[error("forbidden: {0}")]
    Forbidden(String),

    /// Internal server error (500).
    #[error("internal error: {0}")]
    Internal(String),

    /// Store error.
    #[error("storage error: {0}")]
    Store(#[from] notebook_store::StoreError),
}

impl ApiError {
    /// Get the error code string for this error.
    pub fn code(&self) -> &'static str {
        match self {
            Self::NotImplemented(_) => "NOT_IMPLEMENTED",
            Self::BadRequest(_) => "BAD_REQUEST",
            Self::NotFound(_) => "NOT_FOUND",
            Self::Unauthorized(_) => "UNAUTHORIZED",
            Self::Forbidden(_) => "FORBIDDEN",
            Self::Internal(_) => "INTERNAL_ERROR",
            Self::Store(_) => "STORAGE_ERROR",
        }
    }

    /// Get the HTTP status code for this error.
    pub fn status_code(&self) -> StatusCode {
        match self {
            Self::NotImplemented(_) => StatusCode::NOT_IMPLEMENTED,
            Self::BadRequest(_) => StatusCode::BAD_REQUEST,
            Self::NotFound(_) => StatusCode::NOT_FOUND,
            Self::Unauthorized(_) => StatusCode::UNAUTHORIZED,
            Self::Forbidden(_) => StatusCode::FORBIDDEN,
            Self::Internal(_) => StatusCode::INTERNAL_SERVER_ERROR,
            Self::Store(e) => match e {
                notebook_store::StoreError::EntryNotFound(_) => StatusCode::NOT_FOUND,
                notebook_store::StoreError::NotebookNotFound(_) => StatusCode::NOT_FOUND,
                notebook_store::StoreError::AuthorNotFound(_) => StatusCode::NOT_FOUND,
                notebook_store::StoreError::PermissionDenied { .. } => StatusCode::FORBIDDEN,
                notebook_store::StoreError::InvalidReference(_) => StatusCode::BAD_REQUEST,
                notebook_store::StoreError::InvalidRevision(_) => StatusCode::BAD_REQUEST,
                notebook_store::StoreError::DuplicateEntry(_) => StatusCode::CONFLICT,
                _ => StatusCode::INTERNAL_SERVER_ERROR,
            },
        }
    }
}

/// JSON error response body.
#[derive(Debug, Serialize)]
pub struct ErrorResponse {
    /// Error details.
    pub error: ErrorDetails,
}

/// Error details within the response.
#[derive(Debug, Serialize)]
pub struct ErrorDetails {
    /// Error code (e.g., "NOT_FOUND", "BAD_REQUEST").
    pub code: String,
    /// Human-readable error message.
    pub message: String,
}

impl IntoResponse for ApiError {
    fn into_response(self) -> Response {
        let status = self.status_code();
        let body = ErrorResponse {
            error: ErrorDetails {
                code: self.code().to_string(),
                message: self.to_string(),
            },
        };

        (status, Json(body)).into_response()
    }
}

/// Result type for API handlers.
pub type ApiResult<T> = Result<T, ApiError>;
