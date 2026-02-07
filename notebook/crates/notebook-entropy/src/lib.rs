//! notebook-entropy: Entropy and integration cost calculations
//!
//! This crate provides:
//! - Information-theoretic entropy metrics for notebook coherence
//! - Integration cost calculation for new entries
//! - Catalog surprise and orphan detection
//! - Topic clustering for knowledge organization
//! - Adaptive threshold calibration for orphan detection
//! - Retroactive cost propagation for affected entries
//! - Full-text search indexing with Tantivy
//! - Catalog generation for dense notebook summaries
//! - Catalog caching with intelligent invalidation
//!
//! ## Modules
//!
//! - [`tfidf`]: TF-IDF text analysis for keyword extraction and document similarity
//! - [`clustering`]: Agglomerative clustering based on keyword similarity
//! - [`coherence`]: Coherence model snapshot for tracking cluster state
//! - [`engine`]: Integration cost computation engine (Task 2-2)
//! - [`calibration`]: Adaptive orphan threshold calibration (Task 2-3)
//! - [`propagation`]: Retroactive cost propagation jobs and worker (Task 2-4)
//! - [`search`]: Full-text search with Tantivy (Task 3-2)
//! - [`catalog`]: Dense catalog generation for BROWSE endpoint (Task 3-1)
//! - [`cache`]: Catalog caching with stale-while-revalidate support (Task 3-4)
//!
//! ## Example Usage
//!
//! ```rust,ignore
//! use notebook_entropy::engine::IntegrationCostEngine;
//! use notebook_entropy::calibration::{ThresholdCalibrator, NotebookConfig};
//! use notebook_core::types::NotebookId;
//!
//! // Create engine and calibrator
//! let mut engine = IntegrationCostEngine::new();
//! let mut calibrator = ThresholdCalibrator::new();
//! let config = NotebookConfig::default(); // auto_calibrate = true
//!
//! // Compute integration cost
//! let cost = engine.compute_cost(&entry, notebook_id)?;
//!
//! // Update calibrator with observation
//! calibrator.observe(cost.catalog_shift);
//!
//! // Check orphan status using adaptive threshold
//! let is_orphan = config.is_orphan(&cost, &calibrator);
//!
//! println!("Entries revised: {}", cost.entries_revised);
//! println!("Catalog shift: {}", cost.catalog_shift);
//! println!("Orphan (adaptive): {}", is_orphan);
//! ```
//!
//! For lower-level coherence operations:
//!
//! ```rust,ignore
//! use notebook_entropy::coherence::CoherenceSnapshot;
//! use notebook_entropy::clustering::ClusteringConfig;
//!
//! // Create coherence model with custom threshold
//! let config = ClusteringConfig {
//!     similarity_threshold: 0.3,
//!     max_clusters: 0,
//! };
//! let mut snapshot = CoherenceSnapshot::with_config(config);
//!
//! // Add entries and track clustering
//! let cluster_id = snapshot.add_entry(&entry);
//!
//! // Query cluster assignment for new entries
//! if let Some(matching_cluster) = snapshot.assign_to_cluster(&new_entry) {
//!     println!("Entry fits cluster {}", matching_cluster);
//! }
//! ```
//!
//! Owned by: agent-coherence (Task 2-1), agent-entropy (Task 2-2), agent-calibration (Task 2-3), agent-search (Task 3-2), agent-catalog (Task 3-1), agent-cache (Task 3-4)

pub use notebook_core;

pub mod cache;
pub mod calibration;
pub mod catalog;
pub mod clustering;
pub mod coherence;
pub mod engine;
pub mod propagation;
pub mod search;
pub mod tfidf;

// Re-export main types for convenience
pub use cache::{
    CacheConfig, CacheStats, CacheStatus, CachedCatalog, CatalogCache, DEFAULT_MAX_AGE_SECS,
    DEFAULT_SHIFT_THRESHOLD,
};
pub use calibration::{NotebookConfig, ThresholdCalibrator};
pub use catalog::{Catalog, CatalogGenerator, ClusterSummary, DEFAULT_MAX_TOKENS};
pub use clustering::{Cluster, ClusterId, ClusteringConfig, ReferenceGraph};
pub use coherence::{CoherenceSnapshot, CoherenceStats};
pub use engine::{EntropyError, IntegrationCostEngine};
pub use propagation::{
    CostUpdater, NoOpCostUpdater, PropagationError, PropagationJob, PropagationQueue,
    PropagationWorker, WorkerStats, create_propagation_job,
};
pub use search::{SearchError, SearchHit, SearchIndex};
pub use tfidf::{CorpusStats, TfIdfVector};
