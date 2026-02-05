//! notebook-core: Core types and primitives for the Knowledge Exchange Platform
//!
//! This crate provides:
//! - Core domain types (Entry, Notebook, Author, etc.)
//! - Cryptographic identity and signing primitives
//! - Axiom contracts and validation traits
//!
//! Owned by: agent-types (types.rs), agent-crypto (crypto.rs, identity.rs)

// Core data types (owned by agent-types)
pub mod types;

// Re-export commonly used types at crate root for convenience
pub use types::{
    ActivityContext, AuthorId, AuthorIdParseError, CausalPosition, Entry, EntryBuilder, EntryId,
    IntegrationCost, Notebook, NotebookId, Participant, Permissions,
};

// Cryptographic primitives (owned by agent-crypto)
pub mod crypto;
pub mod identity;
