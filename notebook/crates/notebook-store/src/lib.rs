//! notebook-store: Storage layer for the Knowledge Exchange Platform
//!
//! This crate provides:
//! - PostgreSQL storage for entries and notebooks
//! - Apache AGE graph queries for reference traversal
//! - Migration management
//! - Type-safe database operations via sqlx
//!
//! # Architecture
//!
//! The storage layer uses PostgreSQL with Apache AGE extension:
//! - Relational tables for entries, notebooks, authors, and access control
//! - Graph database for reference traversal and semantic links
//!
//! # Usage
//!
//! ```rust,ignore
//! use notebook_store::{Store, StoreConfig};
//!
//! let config = StoreConfig::from_env()?;
//! let store = Store::connect(config).await?;
//!
//! // Insert an entry
//! store.insert_entry(&entry).await?;
//!
//! // Query entries
//! let entries = store.get_entries_by_notebook(notebook_id, None, 100).await?;
//! ```
//!
//! Owned by: agent-store

pub mod causal;
pub mod error;
pub mod graph;
pub mod models;
pub mod queries;
pub mod repository;
pub mod schema;
pub mod store;

pub use causal::CausalPositionService;
pub use error::{StoreError, StoreResult};
pub use models::*;
pub use queries::{
    AuthorEntriesQuery, BatchEntryQuery, BrokenReferencesQuery, NotebookStats,
    NotebookStatsQuery, OrphanEntriesQuery, TopicQuery,
};
pub use repository::{AuthorPublicKey, Repository, StoreEntryInput, DEFAULT_MAX_DEPTH};
pub use store::{Store, StoreConfig};

// Re-export notebook-core for downstream crates
pub use notebook_core;
