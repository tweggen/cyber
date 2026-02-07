//! Repository layer providing domain-typed interfaces to the storage layer.
//!
//! This module wraps the raw Store operations with notebook-core types,
//! providing a clean interface for application code. It handles:
//!
//! - Type conversions between database rows and domain types
//! - Cycle-safe graph traversal with depth limits
//! - Reference resolution and batch fetching
//!
//! Owned by: agent-storage

use notebook_core::{
    ActivityContext, AuthorId, CausalPosition, Entry, EntryId, IntegrationCost, Notebook,
    NotebookId, Participant, Permissions,
};
use uuid::Uuid;

use crate::Store;
use crate::error::{StoreError, StoreResult};
use crate::models::{EntryRow, IntegrationCostJson, NewAuthor, NewEntry, NewNotebook};

/// Default maximum depth for recursive graph traversal.
pub const DEFAULT_MAX_DEPTH: u32 = 100;

/// Repository providing domain-typed access to the store.
///
/// Wraps the raw Store with notebook-core types and provides
/// cycle-safe traversal for reference queries.
#[derive(Debug, Clone)]
pub struct Repository {
    store: Store,
    /// Maximum depth for recursive traversal (default: 100)
    max_depth: u32,
}

impl Repository {
    /// Create a new repository wrapping the given store.
    pub fn new(store: Store) -> Self {
        Self {
            store,
            max_depth: DEFAULT_MAX_DEPTH,
        }
    }

    /// Create a repository with custom max depth.
    pub fn with_max_depth(store: Store, max_depth: u32) -> Self {
        Self { store, max_depth }
    }

    /// Get reference to the underlying store.
    pub fn store(&self) -> &Store {
        &self.store
    }

    // ========================================================================
    // Entry Operations
    // ========================================================================

    /// Store a new entry in the notebook.
    ///
    /// Takes a domain Entry and stores it, returning the assigned EntryId.
    /// The entry must have valid references (all referenced entries must exist)
    /// and a valid signature (64 bytes).
    pub async fn store_entry(&self, entry: &Entry) -> StoreResult<EntryId> {
        // Convert domain Entry to NewEntry
        let new_entry = self.entry_to_new_entry(entry)?;

        // Store and return the ID
        let row = self.store.insert_entry(&new_entry).await?;
        Ok(EntryId::from_uuid(row.id))
    }

    /// Get an entry by its ID.
    ///
    /// Returns the full Entry with all metadata including causal position
    /// and integration cost.
    pub async fn get_entry(&self, id: EntryId) -> StoreResult<Entry> {
        let row = self.store.get_entry(id.0).await?;
        self.entry_row_to_entry(&row).await
    }

    /// Get a specific revision of an entry by revision number.
    ///
    /// Revision 0 is the original entry, revision 1 is the first revision, etc.
    /// This follows the revision_of chain from the given entry.
    pub async fn get_entry_revision(&self, id: EntryId, revision: u32) -> StoreResult<Entry> {
        // Get the entry itself
        let entry = self.store.get_entry(id.0).await?;

        if revision == 0 {
            // Revision 0 is the current entry
            return self.entry_row_to_entry(&entry).await;
        }

        // Get the revision chain
        let chain = self.store.get_revisions(id.0).await?;

        // Find the requested revision
        // Chain is ordered by depth, so index (revision-1) is what we want
        let target_idx = (revision - 1) as usize;
        if target_idx >= chain.len() {
            return Err(StoreError::EntryNotFound(id.0));
        }

        self.entry_row_to_entry(&chain[target_idx]).await
    }

    /// Get the full revision chain for an entry.
    ///
    /// Returns all entries in the revision chain, starting from the
    /// immediate predecessor and going back to the original.
    /// Uses cycle-safe traversal with depth limits.
    pub async fn get_revision_chain(&self, id: EntryId) -> StoreResult<Vec<Entry>> {
        let rows = self.store.get_revisions(id.0).await?;

        let mut entries = Vec::with_capacity(rows.len());
        for row in rows {
            entries.push(self.entry_row_to_entry(&row).await?);
        }
        Ok(entries)
    }

    /// Get all entries that this entry references.
    ///
    /// Returns the direct references (depth 1). For recursive traversal,
    /// use get_reference_closure.
    pub async fn get_references(&self, id: EntryId) -> StoreResult<Vec<Entry>> {
        let entry = self.store.get_entry(id.0).await?;

        let mut entries = Vec::with_capacity(entry.references.len());
        for ref_id in &entry.references {
            match self.store.get_entry(*ref_id).await {
                Ok(row) => entries.push(self.entry_row_to_entry(&row).await?),
                Err(StoreError::EntryNotFound(_)) => {
                    // Referenced entry was deleted - skip it
                    tracing::warn!("Referenced entry {} not found", ref_id);
                }
                Err(e) => return Err(e),
            }
        }
        Ok(entries)
    }

    /// Get all entries that reference this entry (citations).
    ///
    /// Returns entries that have this entry in their references array.
    pub async fn get_referencing(&self, id: EntryId) -> StoreResult<Vec<Entry>> {
        let rows = self.store.get_entries_referencing(id.0).await?;

        let mut entries = Vec::with_capacity(rows.len());
        for row in rows {
            entries.push(self.entry_row_to_entry(&row).await?);
        }
        Ok(entries)
    }

    /// Get the transitive closure of references with cycle detection.
    ///
    /// Returns all entries reachable via references, with their depth
    /// from the starting entry. Uses AGE graph queries when available,
    /// otherwise falls back to SQL recursive CTEs transparently.
    pub async fn get_reference_closure(
        &self,
        id: EntryId,
        max_depth: Option<u32>,
    ) -> StoreResult<Vec<(Entry, u32)>> {
        let depth = max_depth.unwrap_or(self.max_depth) as i32;

        let ids_with_depth = self
            .store
            .graph()
            .find_reference_closure(id.0, depth)
            .await?;

        let mut entries = Vec::with_capacity(ids_with_depth.len());
        for (ref_id, d) in ids_with_depth {
            match self.store.get_entry(ref_id).await {
                Ok(row) => {
                    let entry = self.entry_row_to_entry(&row).await?;
                    entries.push((entry, d as u32));
                }
                Err(StoreError::EntryNotFound(_)) => {
                    tracing::warn!("Entry {} in graph/index but not in table", ref_id);
                }
                Err(e) => return Err(e),
            }
        }
        Ok(entries)
    }

    // ========================================================================
    // Notebook Operations
    // ========================================================================

    /// Create a new notebook.
    ///
    /// The owner must already be registered as an author.
    /// Returns the NotebookId of the created notebook.
    pub async fn create_notebook(&self, name: &str, owner: AuthorId) -> StoreResult<NotebookId> {
        let new_notebook = NewNotebook::new(name.to_string(), owner.0);
        let row = self.store.insert_notebook(&new_notebook).await?;
        Ok(NotebookId::from_uuid(row.id))
    }

    /// Get a notebook by ID.
    ///
    /// Returns the full Notebook with participants.
    pub async fn get_notebook(&self, id: NotebookId) -> StoreResult<Notebook> {
        let row = self.store.get_notebook(id.0).await?;

        // Get access list to build participants
        let access_list = self.store.list_notebook_access(id.0).await?;

        let mut participants: Vec<Participant> = Vec::with_capacity(access_list.len());
        for access in access_list {
            if access.author_id.len() == 32 {
                let mut author_bytes = [0u8; 32];
                author_bytes.copy_from_slice(&access.author_id);
                participants.push(Participant {
                    entity: AuthorId::from_bytes(author_bytes),
                    permissions: Permissions {
                        read: access.read,
                        write: access.write,
                    },
                });
            }
        }

        // Get owner AuthorId
        let owner_bytes: [u8; 32] = row.owner_id.as_slice().try_into().map_err(|_| {
            StoreError::ConfigError("Invalid owner_id length in database".to_string())
        })?;

        Ok(Notebook {
            id: NotebookId::from_uuid(row.id),
            name: row.name,
            owner: AuthorId::from_bytes(owner_bytes),
            participants,
        })
    }

    // ========================================================================
    // Author Operations
    // ========================================================================

    /// Register a new author with their public key.
    ///
    /// The AuthorId is computed as the BLAKE3 hash of the public key.
    /// Returns the computed AuthorId.
    ///
    /// Note: This uses BLAKE3 per the architecture spec. The actual
    /// hashing is done here since the crypto module may not be available.
    pub async fn register_author(&self, public_key: &[u8]) -> StoreResult<AuthorId> {
        if public_key.len() != 32 {
            return Err(StoreError::InvalidPublicKeyLength(public_key.len()));
        }

        // Compute AuthorId as BLAKE3 hash of public key
        let author_id_bytes = blake3::hash(public_key);
        let author_id_arr: [u8; 32] = *author_id_bytes.as_bytes();

        let mut pk_arr = [0u8; 32];
        pk_arr.copy_from_slice(public_key);

        let new_author = NewAuthor::new(author_id_arr, pk_arr);
        self.store.insert_author(&new_author).await?;

        Ok(AuthorId::from_bytes(author_id_arr))
    }

    /// Get an author by their AuthorId.
    ///
    /// Returns the public key associated with the author.
    pub async fn get_author(&self, id: AuthorId) -> StoreResult<AuthorPublicKey> {
        let row = self.store.get_author(&id.0).await?;

        let public_key: [u8; 32] = row.public_key.as_slice().try_into().map_err(|_| {
            StoreError::ConfigError("Invalid public_key length in database".to_string())
        })?;

        Ok(AuthorPublicKey {
            id,
            public_key,
            created: row.created,
        })
    }

    // ========================================================================
    // Internal Conversions
    // ========================================================================

    /// Convert a domain Entry to a NewEntry for storage.
    fn entry_to_new_entry(&self, entry: &Entry) -> StoreResult<NewEntry> {
        Ok(NewEntry {
            id: entry.id.0,
            notebook_id: Uuid::nil(), // Will be set by caller or from context
            content: entry.content.clone(),
            content_type: entry.content_type.clone(),
            topic: entry.topic.clone(),
            author_id: entry.author.0,
            signature: entry.signature.clone(),
            revision_of: entry.revision_of.map(|e| e.0),
            references: entry.references.iter().map(|e| e.0).collect(),
            integration_cost: IntegrationCostJson::from(entry.integration_cost),
        })
    }

    /// Convert an EntryRow to a domain Entry.
    async fn entry_row_to_entry(&self, row: &EntryRow) -> StoreResult<Entry> {
        // Parse author_id
        let author_bytes: [u8; 32] = row.author_id.as_slice().try_into().map_err(|_| {
            StoreError::ConfigError("Invalid author_id length in database".to_string())
        })?;

        // Parse integration cost
        let integration_cost_json = row.parse_integration_cost()?;

        // Get activity context for causal position
        let (entries_since, total_entries) = self
            .store
            .get_activity_context(row.notebook_id, &author_bytes)
            .await?;

        let recent_entropy = self.store.get_recent_entropy(row.notebook_id).await?;

        let activity_context = ActivityContext {
            entries_since_last_by_author: entries_since,
            total_notebook_entries: total_entries,
            recent_entropy,
        };

        Ok(Entry {
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
                activity_context,
            },
            created: row.created,
            integration_cost: IntegrationCost::from(integration_cost_json),
        })
    }
}

/// Author information returned from get_author.
#[derive(Debug, Clone)]
pub struct AuthorPublicKey {
    /// The author's identity.
    pub id: AuthorId,
    /// The author's Ed25519 public key (32 bytes).
    pub public_key: [u8; 32],
    /// When the author was registered.
    pub created: chrono::DateTime<chrono::Utc>,
}

/// Input for storing an entry with notebook context.
#[derive(Debug, Clone)]
pub struct StoreEntryInput {
    /// The entry to store.
    pub entry: Entry,
    /// The notebook to store it in.
    pub notebook_id: NotebookId,
}

impl Repository {
    /// Store an entry with explicit notebook context.
    ///
    /// This is the preferred method when you need to specify
    /// which notebook the entry belongs to.
    pub async fn store_entry_in_notebook(&self, input: &StoreEntryInput) -> StoreResult<EntryId> {
        let mut new_entry = self.entry_to_new_entry(&input.entry)?;
        new_entry.notebook_id = input.notebook_id.0;

        let row = self.store.insert_entry(&new_entry).await?;
        Ok(EntryId::from_uuid(row.id))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_max_depth() {
        assert_eq!(DEFAULT_MAX_DEPTH, 100);
    }
}
