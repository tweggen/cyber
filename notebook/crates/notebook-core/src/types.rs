//! Core data types for the Knowledge Exchange Platform.
//!
//! This module defines the fundamental types used throughout the platform,
//! implementing the axioms defined in the foundation specification:
//!
//! - Each entry consists of a content blob (representation-agnostic)
//! - Content-type declaration (open registry like MIME)
//! - Authorship (cryptographically signed)
//! - Causal context (references to prior entries, cyclic graph allowed)
//! - Integration cost (system-computed, not author-declared)
//!
//! All types derive `Debug`, `Clone`, `Serialize`, and `Deserialize` for
//! inspection, copying, and JSON serialization.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::fmt;
use std::str::FromStr;
use uuid::Uuid;

// ============================================================================
// ID Types
// ============================================================================

/// Unique identifier for an entry in the notebook.
///
/// Wraps a UUID v4, providing type safety to distinguish entry IDs from other
/// UUID-based identifiers in the system.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct EntryId(pub Uuid);

impl EntryId {
    /// Creates a new random EntryId using UUID v4.
    #[must_use]
    pub fn new() -> Self {
        Self(Uuid::new_v4())
    }

    /// Creates an EntryId from an existing UUID.
    #[must_use]
    pub const fn from_uuid(uuid: Uuid) -> Self {
        Self(uuid)
    }

    /// Returns the inner UUID.
    #[must_use]
    pub const fn as_uuid(&self) -> &Uuid {
        &self.0
    }
}

impl Default for EntryId {
    fn default() -> Self {
        Self::new()
    }
}

impl fmt::Display for EntryId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl FromStr for EntryId {
    type Err = uuid::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Ok(Self(Uuid::parse_str(s)?))
    }
}

/// Unique identifier for a notebook.
///
/// Wraps a UUID v4, providing type safety to distinguish notebook IDs from
/// other UUID-based identifiers in the system.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct NotebookId(pub Uuid);

impl NotebookId {
    /// Creates a new random NotebookId using UUID v4.
    #[must_use]
    pub fn new() -> Self {
        Self(Uuid::new_v4())
    }

    /// Creates a NotebookId from an existing UUID.
    #[must_use]
    pub const fn from_uuid(uuid: Uuid) -> Self {
        Self(uuid)
    }

    /// Returns the inner UUID.
    #[must_use]
    pub const fn as_uuid(&self) -> &Uuid {
        &self.0
    }
}

impl Default for NotebookId {
    fn default() -> Self {
        Self::new()
    }
}

impl fmt::Display for NotebookId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl FromStr for NotebookId {
    type Err = uuid::Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Ok(Self(Uuid::parse_str(s)?))
    }
}

/// Unique identifier for an author, derived from their public key.
///
/// This is a 32-byte hash (SHA-256) of the author's Ed25519 public key.
/// The actual derivation from a public key is handled by the crypto module
/// (owned by agent-crypto). This type only defines the storage and
/// serialization format.
#[derive(Clone, Copy, PartialEq, Eq, Hash)]
pub struct AuthorId(pub [u8; 32]);

impl AuthorId {
    /// Creates an AuthorId from a 32-byte array.
    #[must_use]
    pub const fn from_bytes(bytes: [u8; 32]) -> Self {
        Self(bytes)
    }

    /// Returns the inner bytes.
    #[must_use]
    pub const fn as_bytes(&self) -> &[u8; 32] {
        &self.0
    }

    /// Creates a zero-filled AuthorId (useful for testing or placeholder).
    #[must_use]
    pub const fn zero() -> Self {
        Self([0u8; 32])
    }
}

impl fmt::Debug for AuthorId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "AuthorId({})", self)
    }
}

impl fmt::Display for AuthorId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        for byte in &self.0 {
            write!(f, "{:02x}", byte)?;
        }
        Ok(())
    }
}

impl FromStr for AuthorId {
    type Err = AuthorIdParseError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        if s.len() != 64 {
            return Err(AuthorIdParseError::InvalidLength(s.len()));
        }

        let mut bytes = [0u8; 32];
        for (i, chunk) in s.as_bytes().chunks(2).enumerate() {
            let hex_str = std::str::from_utf8(chunk).map_err(|_| AuthorIdParseError::InvalidHex)?;
            bytes[i] =
                u8::from_str_radix(hex_str, 16).map_err(|_| AuthorIdParseError::InvalidHex)?;
        }
        Ok(Self(bytes))
    }
}

/// Error type for parsing AuthorId from string.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum AuthorIdParseError {
    /// The hex string had an invalid length (expected 64 characters).
    InvalidLength(usize),
    /// The string contained invalid hex characters.
    InvalidHex,
}

impl fmt::Display for AuthorIdParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidLength(len) => {
                write!(
                    f,
                    "invalid AuthorId length: expected 64 hex chars, got {}",
                    len
                )
            }
            Self::InvalidHex => write!(f, "invalid hex character in AuthorId"),
        }
    }
}

impl std::error::Error for AuthorIdParseError {}

impl Serialize for AuthorId {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_str(&self.to_string())
    }
}

impl<'de> Deserialize<'de> for AuthorId {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let s = String::deserialize(deserializer)?;
        Self::from_str(&s).map_err(serde::de::Error::custom)
    }
}

// ============================================================================
// Cost and Position Types
// ============================================================================

/// Measures how much the notebook must reorganize to accommodate an entry.
///
/// Integration cost is system-computed (not author-declared) per the platform
/// axioms. It helps readers understand the "weight" of an entry in terms of
/// its impact on the notebook's structure.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct IntegrationCost {
    /// Number of existing entries that needed revision due to this entry.
    pub entries_revised: u32,

    /// Number of references that were broken by this entry.
    pub references_broken: u32,

    /// Measure of how much the catalog/index had to shift (0.0 to 1.0 typical).
    pub catalog_shift: f64,

    /// Whether this entry has no incoming references (is an orphan).
    pub orphan: bool,
}

impl IntegrationCost {
    /// Creates a new IntegrationCost with all fields set to zero/false.
    #[must_use]
    pub const fn zero() -> Self {
        Self {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.0,
            orphan: false,
        }
    }
}

impl Default for IntegrationCost {
    fn default() -> Self {
        Self::zero()
    }
}

/// Contextual information about activity when an entry was created.
///
/// This helps establish the causal position of an entry by capturing
/// the state of notebook activity at creation time.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct ActivityContext {
    /// Number of entries since the author's last entry in this notebook.
    pub entries_since_last_by_author: u32,

    /// Total number of entries in the notebook at creation time.
    pub total_notebook_entries: u32,

    /// Recent entropy measure from the notebook-entropy crate.
    /// Higher values indicate more diverse/chaotic recent activity.
    pub recent_entropy: f64,
}

impl ActivityContext {
    /// Creates a new ActivityContext for the first entry by an author.
    #[must_use]
    pub const fn first_entry() -> Self {
        Self {
            entries_since_last_by_author: 0,
            total_notebook_entries: 0,
            recent_entropy: 0.0,
        }
    }
}

impl Default for ActivityContext {
    fn default() -> Self {
        Self::first_entry()
    }
}

/// Establishes the causal position of an entry within the notebook.
///
/// Combines a monotonic sequence number with activity context to provide
/// a rich understanding of when and under what conditions an entry was created.
#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
pub struct CausalPosition {
    /// Monotonically increasing sequence number within the notebook.
    pub sequence: u64,

    /// Activity context at the time of entry creation.
    pub activity_context: ActivityContext,
}

impl CausalPosition {
    /// Creates a CausalPosition for the first entry in a notebook.
    #[must_use]
    pub const fn first() -> Self {
        Self {
            sequence: 1,
            activity_context: ActivityContext::first_entry(),
        }
    }
}

impl Default for CausalPosition {
    fn default() -> Self {
        Self::first()
    }
}

// ============================================================================
// Permission Types
// ============================================================================

/// Defines what actions an entity can perform on a notebook.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub struct Permissions {
    /// Whether the entity can read entries from the notebook.
    pub read: bool,

    /// Whether the entity can write entries to the notebook.
    pub write: bool,
}

impl Permissions {
    /// Full permissions (read and write).
    #[must_use]
    pub const fn full() -> Self {
        Self {
            read: true,
            write: true,
        }
    }

    /// Read-only permissions.
    #[must_use]
    pub const fn read_only() -> Self {
        Self {
            read: true,
            write: false,
        }
    }

    /// No permissions.
    #[must_use]
    pub const fn none() -> Self {
        Self {
            read: false,
            write: false,
        }
    }
}

impl Default for Permissions {
    fn default() -> Self {
        Self::read_only()
    }
}

/// A participant in a notebook with their associated permissions.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Participant {
    /// The author identity of the participant.
    pub entity: AuthorId,

    /// What the participant can do in the notebook.
    pub permissions: Permissions,
}

// ============================================================================
// Core Domain Types
// ============================================================================

/// A notebook is a collaborative knowledge space containing entries.
///
/// Notebooks have an owner and a list of participants with varying permissions.
/// The owner always has full permissions.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Notebook {
    /// Unique identifier for this notebook.
    pub id: NotebookId,

    /// Human-readable name for the notebook.
    pub name: String,

    /// The author who owns this notebook (always has full permissions).
    pub owner: AuthorId,

    /// List of participants and their permissions.
    pub participants: Vec<Participant>,
}

impl Notebook {
    /// Creates a new notebook with the given name and owner.
    ///
    /// The owner is automatically added as a participant with full permissions.
    #[must_use]
    pub fn new(name: impl Into<String>, owner: AuthorId) -> Self {
        Self {
            id: NotebookId::new(),
            name: name.into(),
            owner,
            participants: vec![Participant {
                entity: owner,
                permissions: Permissions::full(),
            }],
        }
    }
}

/// An entry in the notebook - the fundamental unit of knowledge exchange.
///
/// Each entry contains:
/// - A content blob (representation-agnostic bytes)
/// - Content-type declaration (MIME-like registry)
/// - Cryptographic authorship proof
/// - Causal context (references to prior entries)
/// - System-computed metadata (position, integration cost)
///
/// Per the platform axioms, entries form a cyclic graph through their
/// references, and integration cost is computed by the system, not declared
/// by the author.
#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct Entry {
    /// Unique identifier for this entry.
    pub id: EntryId,

    /// The actual content as raw bytes (representation-agnostic per axioms).
    pub content: Vec<u8>,

    /// MIME-like content type (e.g., "text/plain", "application/json").
    pub content_type: String,

    /// Optional topic/category for the entry.
    pub topic: Option<String>,

    /// The author's identity (derived from their public key).
    pub author: AuthorId,

    /// Ed25519 signature of the entry content (64 bytes typically).
    /// The signature scheme is defined in the crypto module (agent-crypto).
    pub signature: Vec<u8>,

    /// References to other entries, establishing causal context.
    /// Cyclic references are allowed per the platform axioms.
    pub references: Vec<EntryId>,

    /// If this entry revises another, the ID of the revised entry.
    pub revision_of: Option<EntryId>,

    /// System-computed causal position in the notebook.
    pub causal_position: CausalPosition,

    /// When the entry was created.
    pub created: DateTime<Utc>,

    /// System-computed cost of integrating this entry.
    pub integration_cost: IntegrationCost,
}

impl Entry {
    /// Creates a builder for constructing an Entry.
    #[must_use]
    pub fn builder() -> EntryBuilder {
        EntryBuilder::default()
    }
}

/// Builder for constructing Entry instances.
///
/// Use this to create entries with explicit control over all fields.
#[derive(Debug, Default)]
pub struct EntryBuilder {
    id: Option<EntryId>,
    content: Vec<u8>,
    content_type: String,
    topic: Option<String>,
    author: Option<AuthorId>,
    signature: Vec<u8>,
    references: Vec<EntryId>,
    revision_of: Option<EntryId>,
    causal_position: Option<CausalPosition>,
    created: Option<DateTime<Utc>>,
    integration_cost: Option<IntegrationCost>,
}

impl EntryBuilder {
    /// Sets the entry ID (generates a new one if not set).
    #[must_use]
    pub fn id(mut self, id: EntryId) -> Self {
        self.id = Some(id);
        self
    }

    /// Sets the content bytes.
    #[must_use]
    pub fn content(mut self, content: impl Into<Vec<u8>>) -> Self {
        self.content = content.into();
        self
    }

    /// Sets the content type.
    #[must_use]
    pub fn content_type(mut self, content_type: impl Into<String>) -> Self {
        self.content_type = content_type.into();
        self
    }

    /// Sets the topic.
    #[must_use]
    pub fn topic(mut self, topic: impl Into<String>) -> Self {
        self.topic = Some(topic.into());
        self
    }

    /// Sets the author.
    #[must_use]
    pub fn author(mut self, author: AuthorId) -> Self {
        self.author = Some(author);
        self
    }

    /// Sets the signature.
    #[must_use]
    pub fn signature(mut self, signature: impl Into<Vec<u8>>) -> Self {
        self.signature = signature.into();
        self
    }

    /// Adds references to other entries.
    #[must_use]
    pub fn references(mut self, refs: impl Into<Vec<EntryId>>) -> Self {
        self.references = refs.into();
        self
    }

    /// Sets the revision_of field.
    #[must_use]
    pub fn revision_of(mut self, id: EntryId) -> Self {
        self.revision_of = Some(id);
        self
    }

    /// Sets the causal position.
    #[must_use]
    pub fn causal_position(mut self, pos: CausalPosition) -> Self {
        self.causal_position = Some(pos);
        self
    }

    /// Sets the creation timestamp.
    #[must_use]
    pub fn created(mut self, created: DateTime<Utc>) -> Self {
        self.created = Some(created);
        self
    }

    /// Sets the integration cost.
    #[must_use]
    pub fn integration_cost(mut self, cost: IntegrationCost) -> Self {
        self.integration_cost = Some(cost);
        self
    }

    /// Builds the Entry, using defaults for unset fields.
    ///
    /// # Panics
    ///
    /// Panics if `author` is not set.
    #[must_use]
    pub fn build(self) -> Entry {
        Entry {
            id: self.id.unwrap_or_default(),
            content: self.content,
            content_type: self.content_type,
            topic: self.topic,
            author: self.author.expect("author is required"),
            signature: self.signature,
            references: self.references,
            revision_of: self.revision_of,
            causal_position: self.causal_position.unwrap_or_default(),
            created: self.created.unwrap_or_else(Utc::now),
            integration_cost: self.integration_cost.unwrap_or_default(),
        }
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn entry_id_roundtrip() {
        let id = EntryId::new();
        let json = serde_json::to_string(&id).unwrap();
        let parsed: EntryId = serde_json::from_str(&json).unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn entry_id_display_fromstr() {
        let id = EntryId::new();
        let s = id.to_string();
        let parsed: EntryId = s.parse().unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn notebook_id_roundtrip() {
        let id = NotebookId::new();
        let json = serde_json::to_string(&id).unwrap();
        let parsed: NotebookId = serde_json::from_str(&json).unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn notebook_id_display_fromstr() {
        let id = NotebookId::new();
        let s = id.to_string();
        let parsed: NotebookId = s.parse().unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn author_id_roundtrip() {
        let bytes = [42u8; 32];
        let id = AuthorId::from_bytes(bytes);
        let json = serde_json::to_string(&id).unwrap();
        let parsed: AuthorId = serde_json::from_str(&json).unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn author_id_display_fromstr() {
        let bytes = [0xab; 32];
        let id = AuthorId::from_bytes(bytes);
        let s = id.to_string();
        assert_eq!(s.len(), 64);
        assert!(s.chars().all(|c| c.is_ascii_hexdigit()));
        let parsed: AuthorId = s.parse().unwrap();
        assert_eq!(id, parsed);
    }

    #[test]
    fn author_id_parse_error_invalid_length() {
        let result: Result<AuthorId, _> = "abc".parse();
        assert!(matches!(result, Err(AuthorIdParseError::InvalidLength(3))));
    }

    #[test]
    fn author_id_parse_error_invalid_hex() {
        let result: Result<AuthorId, _> = "zz".repeat(32).parse();
        assert!(matches!(result, Err(AuthorIdParseError::InvalidHex)));
    }

    #[test]
    fn integration_cost_roundtrip() {
        let cost = IntegrationCost {
            entries_revised: 5,
            references_broken: 2,
            catalog_shift: 0.75,
            orphan: true,
        };
        let json = serde_json::to_string(&cost).unwrap();
        let parsed: IntegrationCost = serde_json::from_str(&json).unwrap();
        assert_eq!(cost, parsed);
    }

    #[test]
    fn activity_context_roundtrip() {
        let ctx = ActivityContext {
            entries_since_last_by_author: 10,
            total_notebook_entries: 100,
            recent_entropy: 2.5,
        };
        let json = serde_json::to_string(&ctx).unwrap();
        let parsed: ActivityContext = serde_json::from_str(&json).unwrap();
        assert_eq!(ctx, parsed);
    }

    #[test]
    fn causal_position_roundtrip() {
        let pos = CausalPosition {
            sequence: 42,
            activity_context: ActivityContext {
                entries_since_last_by_author: 5,
                total_notebook_entries: 50,
                recent_entropy: 1.5,
            },
        };
        let json = serde_json::to_string(&pos).unwrap();
        let parsed: CausalPosition = serde_json::from_str(&json).unwrap();
        assert_eq!(pos, parsed);
    }

    #[test]
    fn permissions_roundtrip() {
        let perms = Permissions::full();
        let json = serde_json::to_string(&perms).unwrap();
        let parsed: Permissions = serde_json::from_str(&json).unwrap();
        assert_eq!(perms, parsed);
    }

    #[test]
    fn participant_roundtrip() {
        let participant = Participant {
            entity: AuthorId::from_bytes([1u8; 32]),
            permissions: Permissions::read_only(),
        };
        let json = serde_json::to_string(&participant).unwrap();
        let parsed: Participant = serde_json::from_str(&json).unwrap();
        assert_eq!(participant, parsed);
    }

    #[test]
    fn notebook_roundtrip() {
        let owner = AuthorId::from_bytes([0xaa; 32]);
        let notebook = Notebook::new("Test Notebook", owner);
        let json = serde_json::to_string(&notebook).unwrap();
        let parsed: Notebook = serde_json::from_str(&json).unwrap();
        assert_eq!(notebook.name, parsed.name);
        assert_eq!(notebook.owner, parsed.owner);
        assert_eq!(notebook.participants.len(), parsed.participants.len());
    }

    #[test]
    fn entry_roundtrip() {
        let author = AuthorId::from_bytes([0xbb; 32]);
        let entry = Entry::builder()
            .content(b"Hello, world!".to_vec())
            .content_type("text/plain")
            .topic("greeting")
            .author(author)
            .signature(vec![0u8; 64])
            .references(vec![])
            .causal_position(CausalPosition::first())
            .integration_cost(IntegrationCost::zero())
            .build();

        let json = serde_json::to_string(&entry).unwrap();
        let parsed: Entry = serde_json::from_str(&json).unwrap();

        assert_eq!(entry.content, parsed.content);
        assert_eq!(entry.content_type, parsed.content_type);
        assert_eq!(entry.topic, parsed.topic);
        assert_eq!(entry.author, parsed.author);
        assert_eq!(entry.signature, parsed.signature);
        assert_eq!(entry.references, parsed.references);
    }

    #[test]
    fn entry_with_references_and_revision() {
        let author = AuthorId::from_bytes([0xcc; 32]);
        let ref1 = EntryId::new();
        let ref2 = EntryId::new();
        let original = EntryId::new();

        let entry = Entry::builder()
            .content(b"Revised content".to_vec())
            .content_type("text/plain")
            .author(author)
            .references(vec![ref1, ref2])
            .revision_of(original)
            .build();

        let json = serde_json::to_string(&entry).unwrap();
        let parsed: Entry = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.references.len(), 2);
        assert_eq!(parsed.revision_of, Some(original));
    }

    #[test]
    fn entry_empty_content() {
        let author = AuthorId::from_bytes([0xdd; 32]);
        let entry = Entry::builder()
            .content(vec![])
            .content_type("application/octet-stream")
            .author(author)
            .build();

        let json = serde_json::to_string(&entry).unwrap();
        let parsed: Entry = serde_json::from_str(&json).unwrap();

        assert!(parsed.content.is_empty());
    }

    #[test]
    fn permissions_variants() {
        assert!(Permissions::full().read);
        assert!(Permissions::full().write);

        assert!(Permissions::read_only().read);
        assert!(!Permissions::read_only().write);

        assert!(!Permissions::none().read);
        assert!(!Permissions::none().write);
    }

    #[test]
    fn integration_cost_default() {
        let cost = IntegrationCost::default();
        assert_eq!(cost.entries_revised, 0);
        assert_eq!(cost.references_broken, 0);
        assert_eq!(cost.catalog_shift, 0.0);
        assert!(!cost.orphan);
    }

    #[test]
    fn author_id_zero() {
        let zero = AuthorId::zero();
        assert_eq!(zero.as_bytes(), &[0u8; 32]);
    }
}
