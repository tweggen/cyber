//! Error types for the storage layer.

use thiserror::Error;
use uuid::Uuid;

/// Result type alias for store operations.
pub type StoreResult<T> = Result<T, StoreError>;

/// Errors that can occur during storage operations.
#[derive(Debug, Error)]
pub enum StoreError {
    /// Database connection error.
    #[error("database connection error: {0}")]
    Connection(#[from] sqlx::Error),

    /// Entry not found.
    #[error("entry not found: {0}")]
    EntryNotFound(Uuid),

    /// Notebook not found.
    #[error("notebook not found: {0}")]
    NotebookNotFound(Uuid),

    /// Author not found.
    #[error("author not found: {0}")]
    AuthorNotFound(Uuid),

    /// Invalid reference - referenced entry does not exist.
    #[error("invalid reference: entry {0} does not exist")]
    InvalidReference(Uuid),

    /// Duplicate entry - entry with this ID already exists.
    #[error("duplicate entry: {0}")]
    DuplicateEntry(Uuid),

    /// Invalid revision - revision_of entry does not exist.
    #[error("invalid revision: entry {0} does not exist")]
    InvalidRevision(Uuid),

    /// Permission denied for the operation.
    #[error("permission denied: {operation} on notebook {notebook_id}")]
    PermissionDenied {
        operation: String,
        notebook_id: Uuid,
    },

    /// Invalid signature length.
    #[error("invalid signature length: expected 64 bytes, got {0}")]
    InvalidSignatureLength(usize),

    /// Invalid public key length.
    #[error("invalid public key length: expected 32 bytes, got {0}")]
    InvalidPublicKeyLength(usize),

    /// Graph operation error.
    #[error("graph operation failed: {0}")]
    GraphError(String),

    /// Migration error.
    #[error("migration error: {0}")]
    MigrationError(String),

    /// Serialization error.
    #[error("serialization error: {0}")]
    SerializationError(#[from] serde_json::Error),

    /// Configuration error.
    #[error("configuration error: {0}")]
    ConfigError(String),
}
