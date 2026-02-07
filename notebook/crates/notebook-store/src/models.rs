//! Database models for the storage layer.
//!
//! These types map directly to database rows and are used for
//! sqlx queries. They are separate from the domain types in
//! notebook-core to allow for database-specific optimizations.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::FromRow;
use uuid::Uuid;

/// Database row for the `authors` table.
///
/// The `id` field is a 32-byte AuthorId (BLAKE3 hash of public key),
/// matching the AuthorId type in notebook-core.
#[derive(Debug, Clone, FromRow)]
pub struct AuthorRow {
    /// AuthorId as 32-byte hash
    pub id: Vec<u8>,
    /// Ed25519 public key (32 bytes)
    pub public_key: Vec<u8>,
    pub created: DateTime<Utc>,
}

impl AuthorRow {
    /// Get the id as a fixed-size array.
    pub fn id_bytes(&self) -> Option<[u8; 32]> {
        if self.id.len() == 32 {
            let mut arr = [0u8; 32];
            arr.copy_from_slice(&self.id);
            Some(arr)
        } else {
            None
        }
    }

    /// Get the public_key as a fixed-size array.
    pub fn public_key_bytes(&self) -> Option<[u8; 32]> {
        if self.public_key.len() == 32 {
            let mut arr = [0u8; 32];
            arr.copy_from_slice(&self.public_key);
            Some(arr)
        } else {
            None
        }
    }
}

/// Database row for the `notebooks` table.
#[derive(Debug, Clone, FromRow)]
pub struct NotebookRow {
    pub id: Uuid,
    pub name: String,
    /// AuthorId as 32-byte hash
    pub owner_id: Vec<u8>,
    pub created: DateTime<Utc>,
}

/// Database row for the `notebook_access` table.
#[derive(Debug, Clone, FromRow)]
pub struct NotebookAccessRow {
    pub notebook_id: Uuid,
    /// AuthorId as 32-byte hash
    pub author_id: Vec<u8>,
    pub read: bool,
    pub write: bool,
    pub granted: DateTime<Utc>,
}

/// Integration cost stored in entries as JSONB.
/// Aligns with IntegrationCost type from notebook-core.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IntegrationCostJson {
    pub entries_revised: u32,
    pub references_broken: u32,
    pub catalog_shift: f64,
    pub orphan: bool,
}

impl Default for IntegrationCostJson {
    fn default() -> Self {
        Self {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.0,
            orphan: false,
        }
    }
}

impl From<notebook_core::IntegrationCost> for IntegrationCostJson {
    fn from(cost: notebook_core::IntegrationCost) -> Self {
        Self {
            entries_revised: cost.entries_revised,
            references_broken: cost.references_broken,
            catalog_shift: cost.catalog_shift,
            orphan: cost.orphan,
        }
    }
}

impl From<IntegrationCostJson> for notebook_core::IntegrationCost {
    fn from(cost: IntegrationCostJson) -> Self {
        Self {
            entries_revised: cost.entries_revised,
            references_broken: cost.references_broken,
            catalog_shift: cost.catalog_shift,
            orphan: cost.orphan,
        }
    }
}

/// Database row for the `entries` table.
#[derive(Debug, Clone, FromRow)]
pub struct EntryRow {
    pub id: Uuid,
    pub notebook_id: Uuid,
    pub content: Vec<u8>,
    pub content_type: String,
    pub topic: Option<String>,
    /// AuthorId as 32-byte hash
    pub author_id: Vec<u8>,
    pub signature: Vec<u8>,
    pub revision_of: Option<Uuid>,
    pub references: Vec<Uuid>,
    pub sequence: i64,
    pub created: DateTime<Utc>,
    pub integration_cost: serde_json::Value,
}

impl EntryRow {
    /// Parse the integration_cost JSONB field.
    pub fn parse_integration_cost(&self) -> Result<IntegrationCostJson, serde_json::Error> {
        serde_json::from_value(self.integration_cost.clone())
    }

    /// Get the author_id as a fixed-size array.
    pub fn author_id_bytes(&self) -> Option<[u8; 32]> {
        if self.author_id.len() == 32 {
            let mut arr = [0u8; 32];
            arr.copy_from_slice(&self.author_id);
            Some(arr)
        } else {
            None
        }
    }
}

/// Input for creating a new author.
#[derive(Debug, Clone)]
pub struct NewAuthor {
    /// AuthorId - 32-byte BLAKE3 hash of public key
    pub id: [u8; 32],
    /// Ed25519 public key (32 bytes)
    pub public_key: [u8; 32],
}

impl NewAuthor {
    /// Create a new author with the given ID and public key.
    pub fn new(id: [u8; 32], public_key: [u8; 32]) -> Self {
        Self { id, public_key }
    }

    /// Create a new author from raw byte vectors.
    ///
    /// Returns None if either vector has incorrect length.
    pub fn from_vecs(id: Vec<u8>, public_key: Vec<u8>) -> Option<Self> {
        if id.len() != 32 || public_key.len() != 32 {
            return None;
        }
        let mut id_arr = [0u8; 32];
        let mut pk_arr = [0u8; 32];
        id_arr.copy_from_slice(&id);
        pk_arr.copy_from_slice(&public_key);
        Some(Self {
            id: id_arr,
            public_key: pk_arr,
        })
    }
}

/// Input for creating a new notebook.
#[derive(Debug, Clone)]
pub struct NewNotebook {
    pub id: Uuid,
    pub name: String,
    /// AuthorId - 32-byte hash
    pub owner_id: [u8; 32],
}

impl NewNotebook {
    pub fn new(name: String, owner_id: [u8; 32]) -> Self {
        Self {
            id: Uuid::new_v4(),
            name,
            owner_id,
        }
    }

    pub fn with_id(id: Uuid, name: String, owner_id: [u8; 32]) -> Self {
        Self { id, name, owner_id }
    }
}

/// Input for granting notebook access.
#[derive(Debug, Clone)]
pub struct NewNotebookAccess {
    pub notebook_id: Uuid,
    /// AuthorId - 32-byte hash
    pub author_id: [u8; 32],
    pub read: bool,
    pub write: bool,
}

/// Input for creating a new entry.
#[derive(Debug, Clone)]
pub struct NewEntry {
    pub id: Uuid,
    pub notebook_id: Uuid,
    pub content: Vec<u8>,
    pub content_type: String,
    pub topic: Option<String>,
    /// AuthorId - 32-byte hash
    pub author_id: [u8; 32],
    pub signature: Vec<u8>,
    pub revision_of: Option<Uuid>,
    pub references: Vec<Uuid>,
    pub integration_cost: IntegrationCostJson,
}

impl NewEntry {
    /// Create a new entry builder.
    pub fn builder(notebook_id: Uuid, author_id: [u8; 32]) -> NewEntryBuilder {
        NewEntryBuilder {
            id: Uuid::new_v4(),
            notebook_id,
            content: Vec::new(),
            content_type: "text/plain".to_string(),
            topic: None,
            author_id,
            signature: vec![0u8; 64], // Placeholder, should be set properly
            revision_of: None,
            references: Vec::new(),
            integration_cost: IntegrationCostJson::default(),
        }
    }
}

/// Builder for NewEntry.
#[derive(Debug, Clone)]
pub struct NewEntryBuilder {
    id: Uuid,
    notebook_id: Uuid,
    content: Vec<u8>,
    content_type: String,
    topic: Option<String>,
    author_id: [u8; 32],
    signature: Vec<u8>,
    revision_of: Option<Uuid>,
    references: Vec<Uuid>,
    integration_cost: IntegrationCostJson,
}

impl NewEntryBuilder {
    pub fn id(mut self, id: Uuid) -> Self {
        self.id = id;
        self
    }

    pub fn content(mut self, content: Vec<u8>) -> Self {
        self.content = content;
        self
    }

    pub fn content_str(mut self, content: &str) -> Self {
        self.content = content.as_bytes().to_vec();
        self
    }

    pub fn content_type(mut self, content_type: String) -> Self {
        self.content_type = content_type;
        self
    }

    pub fn topic(mut self, topic: Option<String>) -> Self {
        self.topic = topic;
        self
    }

    pub fn signature(mut self, signature: Vec<u8>) -> Self {
        self.signature = signature;
        self
    }

    pub fn revision_of(mut self, revision_of: Option<Uuid>) -> Self {
        self.revision_of = revision_of;
        self
    }

    pub fn references(mut self, references: Vec<Uuid>) -> Self {
        self.references = references;
        self
    }

    pub fn integration_cost(mut self, cost: IntegrationCostJson) -> Self {
        self.integration_cost = cost;
        self
    }

    pub fn build(self) -> NewEntry {
        NewEntry {
            id: self.id,
            notebook_id: self.notebook_id,
            content: self.content,
            content_type: self.content_type,
            topic: self.topic,
            author_id: self.author_id,
            signature: self.signature,
            revision_of: self.revision_of,
            references: self.references,
            integration_cost: self.integration_cost,
        }
    }
}

// ==================== User Management Models ====================

/// Database row for the `users` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserRow {
    pub id: Uuid,
    pub username: String,
    pub display_name: Option<String>,
    pub password_hash: String,
    pub author_id: Vec<u8>,
    pub role: String,
    pub is_active: bool,
    pub created: DateTime<Utc>,
    pub updated: DateTime<Utc>,
}

/// Input for creating a new user.
#[derive(Debug, Clone)]
pub struct NewUser {
    pub username: String,
    pub display_name: Option<String>,
    pub password_hash: String,
    pub author_id: [u8; 32],
    pub role: String,
}

/// Database row for the `user_keys` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserKeyRow {
    pub user_id: Uuid,
    pub encrypted_private_key: Vec<u8>,
}

/// Database row for the `user_quotas` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserQuotaRow {
    pub user_id: Uuid,
    pub max_notebooks: i32,
    pub max_entries_per_notebook: i32,
    pub max_entry_size_bytes: i32,
    pub max_total_storage_bytes: i64,
}

/// Database row for the `usage_log` table.
#[derive(Debug, Clone, FromRow)]
pub struct UsageLogRow {
    pub id: i64,
    pub user_id: Option<Uuid>,
    pub author_id: Vec<u8>,
    pub action: String,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    pub details: Option<serde_json::Value>,
    pub ip_address: Option<String>,
    pub created: DateTime<Utc>,
}

/// Input for creating a new usage log entry.
#[derive(Debug, Clone)]
pub struct NewUsageLogEntry {
    pub user_id: Option<Uuid>,
    pub author_id: [u8; 32],
    pub action: String,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    pub details: Option<serde_json::Value>,
    pub ip_address: Option<String>,
}

/// Query parameters for listing entries.
#[derive(Debug, Clone, Default)]
pub struct EntryQuery {
    /// Filter by notebook ID (required for most queries).
    pub notebook_id: Option<Uuid>,
    /// Filter by topic.
    pub topic: Option<String>,
    /// Filter by author (32-byte AuthorId).
    pub author_id: Option<[u8; 32]>,
    /// Start from this sequence number (exclusive).
    pub after_sequence: Option<i64>,
    /// Maximum number of entries to return.
    pub limit: Option<i64>,
    /// Order by sequence descending (newest first).
    pub newest_first: bool,
}

impl EntryQuery {
    pub fn new(notebook_id: Uuid) -> Self {
        Self {
            notebook_id: Some(notebook_id),
            ..Default::default()
        }
    }

    pub fn topic(mut self, topic: String) -> Self {
        self.topic = Some(topic);
        self
    }

    pub fn author(mut self, author_id: [u8; 32]) -> Self {
        self.author_id = Some(author_id);
        self
    }

    pub fn after(mut self, sequence: i64) -> Self {
        self.after_sequence = Some(sequence);
        self
    }

    pub fn limit(mut self, limit: i64) -> Self {
        self.limit = Some(limit);
        self
    }

    pub fn newest_first(mut self) -> Self {
        self.newest_first = true;
        self
    }
}
