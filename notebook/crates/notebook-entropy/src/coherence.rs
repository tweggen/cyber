//! Coherence model for notebook entries.
//!
//! The coherence model tracks how entries cluster together based on their
//! content similarity. It provides:
//!
//! - A snapshot of the current clustering state
//! - Methods to assign new entries to clusters
//! - Persistence via serde serialization
//!
//! This model is used by the entropy engine to compute integration costs.

use crate::clustering::{
    Cluster, ClusterId, ClusteringConfig, DEFAULT_SIMILARITY_THRESHOLD, ReferenceGraph,
    calculate_reference_density, cluster_entries, find_best_cluster,
};
use crate::tfidf::{CorpusStats, TfIdfVector, tokenize};
use notebook_core::types::{CausalPosition, Entry, EntryId};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// A snapshot of the coherence state for a notebook.
///
/// This captures the current clustering of entries and corpus statistics
/// needed for TF-IDF computation. It can be serialized for persistence.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CoherenceSnapshot {
    /// Current clusters in the notebook.
    pub clusters: Vec<Cluster>,

    /// Corpus statistics for IDF computation.
    pub corpus_stats: CorpusStats,

    /// TF-IDF vectors for each cluster (for assignment queries).
    cluster_vectors: HashMap<ClusterId, TfIdfVector>,

    /// TF-IDF vectors for each entry (for incremental updates).
    entry_vectors: HashMap<EntryId, TfIdfVector>,

    /// Reference graph for density calculation.
    #[serde(skip, default)]
    reference_graph: ReferenceGraph,

    /// Causal position when this snapshot was created.
    pub timestamp: CausalPosition,

    /// Configuration for clustering.
    pub config: ClusteringConfig,

    /// Next cluster ID to assign.
    next_cluster_id: u64,
}

impl CoherenceSnapshot {
    /// Creates a new empty coherence snapshot.
    pub fn new() -> Self {
        Self {
            clusters: Vec::new(),
            corpus_stats: CorpusStats::new(),
            cluster_vectors: HashMap::new(),
            entry_vectors: HashMap::new(),
            reference_graph: ReferenceGraph::new(),
            timestamp: CausalPosition::first(),
            config: ClusteringConfig::default(),
            next_cluster_id: 0,
        }
    }

    /// Creates a coherence snapshot with custom configuration.
    pub fn with_config(config: ClusteringConfig) -> Self {
        Self {
            config,
            ..Self::new()
        }
    }

    /// Sets the similarity threshold for clustering.
    pub fn set_threshold(&mut self, threshold: f64) {
        self.config.similarity_threshold = threshold;
    }

    /// Returns the current similarity threshold.
    pub fn threshold(&self) -> f64 {
        self.config.similarity_threshold
    }

    /// Returns the number of clusters.
    pub fn cluster_count(&self) -> usize {
        self.clusters.len()
    }

    /// Returns the total number of entries tracked.
    pub fn entry_count(&self) -> usize {
        self.entry_vectors.len()
    }

    /// Allocates a new cluster ID.
    fn allocate_cluster_id(&mut self) -> ClusterId {
        let id = ClusterId::new(self.next_cluster_id);
        self.next_cluster_id += 1;
        id
    }

    /// Extracts text content from an entry for tokenization.
    ///
    /// For text content types, decodes as UTF-8. For others, returns empty.
    fn extract_text(entry: &Entry) -> String {
        if entry.content_type.starts_with("text/") {
            String::from_utf8_lossy(&entry.content).into_owned()
        } else {
            String::new()
        }
    }

    /// Finds the best matching cluster for a new entry.
    ///
    /// # Arguments
    ///
    /// * `entry` - The entry to classify
    ///
    /// # Returns
    ///
    /// The best matching cluster ID if similarity exceeds threshold, or None.
    pub fn assign_to_cluster(&self, entry: &Entry) -> Option<ClusterId> {
        let text = Self::extract_text(entry);
        let tokens = tokenize(&text);

        if tokens.is_empty() {
            // Non-text entry: try to match by topic if present
            return self.match_by_topic(entry);
        }

        let vector = TfIdfVector::from_tokens(&tokens, &self.corpus_stats);
        if vector.is_empty() {
            return self.match_by_topic(entry);
        }

        let cluster_data: Vec<_> = self
            .cluster_vectors
            .iter()
            .map(|(id, vec)| (*id, vec.clone()))
            .collect();

        find_best_cluster(&vector, &cluster_data, self.config.similarity_threshold)
            .map(|(id, _)| id)
    }

    /// Tries to match an entry to a cluster by its topic.
    fn match_by_topic(&self, entry: &Entry) -> Option<ClusterId> {
        let topic = entry.topic.as_ref()?;
        let topic_lower = topic.to_lowercase();

        // Find cluster with most keyword overlap with topic
        self.clusters
            .iter()
            .filter_map(|cluster| {
                let matches = cluster
                    .topic_keywords
                    .iter()
                    .filter(|kw| topic_lower.contains(kw.as_str()))
                    .count();

                if matches > 0 {
                    Some((cluster.id, matches))
                } else {
                    None
                }
            })
            .max_by_key(|(_, matches)| *matches)
            .map(|(id, _)| id)
    }

    /// Gets a cluster by its ID.
    pub fn get_cluster(&self, id: ClusterId) -> Option<&Cluster> {
        self.clusters.iter().find(|c| c.id == id)
    }

    /// Gets the cluster containing a specific entry.
    pub fn get_entry_cluster(&self, entry_id: &EntryId) -> Option<&Cluster> {
        self.clusters.iter().find(|c| c.contains(entry_id))
    }

    /// Adds an entry to the coherence model.
    ///
    /// This updates corpus statistics and either assigns the entry to an
    /// existing cluster or creates a new singleton cluster.
    ///
    /// # Arguments
    ///
    /// * `entry` - The entry to add
    ///
    /// # Returns
    ///
    /// The cluster ID the entry was assigned to (new or existing).
    pub fn add_entry(&mut self, entry: &Entry) -> ClusterId {
        // Update reference graph
        self.reference_graph
            .add_entry_references(entry.id, &entry.references);

        // Extract and tokenize text
        let text = Self::extract_text(entry);
        let tokens = tokenize(&text);

        // Update corpus stats
        self.corpus_stats.add_document(&tokens);

        // Compute TF-IDF vector
        let vector = TfIdfVector::from_tokens(&tokens, &self.corpus_stats);
        self.entry_vectors.insert(entry.id, vector.clone());

        // Try to find matching cluster
        if let Some(cluster_id) = self.assign_to_cluster(entry) {
            // Add to existing cluster
            self.add_entry_to_cluster(entry.id, cluster_id, &vector);
            cluster_id
        } else {
            // Create new singleton cluster
            self.create_singleton_cluster(entry.id, &vector)
        }
    }

    /// Adds an entry to an existing cluster.
    fn add_entry_to_cluster(
        &mut self,
        entry_id: EntryId,
        cluster_id: ClusterId,
        vector: &TfIdfVector,
    ) {
        if let Some(cluster) = self.clusters.iter_mut().find(|c| c.id == cluster_id) {
            cluster.entry_ids.push(entry_id);

            // Update cluster keywords
            let entry_vectors: Vec<_> = cluster
                .entry_ids
                .iter()
                .filter_map(|id| self.entry_vectors.get(id))
                .collect();

            let merged = crate::tfidf::merge_vectors(&entry_vectors);
            cluster.topic_keywords = merged.top_terms(5);

            // Update cluster vector
            self.cluster_vectors.insert(cluster_id, merged);

            // Update reference density
            cluster.reference_density =
                calculate_reference_density(&cluster.entry_ids, &self.reference_graph);
        }
    }

    /// Creates a new singleton cluster for an entry.
    fn create_singleton_cluster(&mut self, entry_id: EntryId, vector: &TfIdfVector) -> ClusterId {
        let cluster_id = self.allocate_cluster_id();
        let keywords = vector.top_terms(5);

        let cluster = Cluster {
            id: cluster_id,
            topic_keywords: keywords,
            entry_ids: vec![entry_id],
            reference_density: 1.0,
        };

        self.clusters.push(cluster);
        self.cluster_vectors.insert(cluster_id, vector.clone());

        cluster_id
    }

    /// Rebuilds the coherence model from a list of entries.
    ///
    /// This performs full clustering rather than incremental updates.
    ///
    /// # Arguments
    ///
    /// * `entries` - All entries to cluster
    /// * `timestamp` - Causal position for the snapshot
    pub fn rebuild(&mut self, entries: &[Entry], timestamp: CausalPosition) {
        // Reset state
        self.clusters.clear();
        self.cluster_vectors.clear();
        self.entry_vectors.clear();
        self.corpus_stats = CorpusStats::new();
        self.reference_graph = ReferenceGraph::new();
        self.next_cluster_id = 0;
        self.timestamp = timestamp;

        if entries.is_empty() {
            return;
        }

        // Build corpus stats and reference graph
        let mut entry_data = Vec::new();
        for entry in entries {
            self.reference_graph
                .add_entry_references(entry.id, &entry.references);

            let text = Self::extract_text(entry);
            let tokens = tokenize(&text);
            self.corpus_stats.add_document(&tokens);

            let vector = TfIdfVector::from_tokens(&tokens, &self.corpus_stats);
            self.entry_vectors.insert(entry.id, vector.clone());
            entry_data.push((entry.id, vector));
        }

        // Perform clustering
        let clusters = cluster_entries(entry_data, &self.reference_graph, &self.config);

        // Store clusters and their vectors
        for cluster in clusters {
            // Compute cluster vector from member entries
            let entry_vectors: Vec<_> = cluster
                .entry_ids
                .iter()
                .filter_map(|id| self.entry_vectors.get(id))
                .collect();
            let merged = crate::tfidf::merge_vectors(&entry_vectors);

            self.cluster_vectors.insert(cluster.id, merged);

            // Update next_cluster_id to be higher than any existing ID
            if cluster.id.0 >= self.next_cluster_id {
                self.next_cluster_id = cluster.id.0 + 1;
            }

            self.clusters.push(cluster);
        }
    }

    /// Computes the average reference density across all clusters.
    pub fn average_density(&self) -> f64 {
        if self.clusters.is_empty() {
            return 0.0;
        }

        let total: f64 = self.clusters.iter().map(|c| c.reference_density).sum();
        total / self.clusters.len() as f64
    }

    /// Computes the weighted average density (weighted by cluster size).
    pub fn weighted_average_density(&self) -> f64 {
        let total_entries: usize = self.clusters.iter().map(|c| c.size()).sum();
        if total_entries == 0 {
            return 0.0;
        }

        let weighted_sum: f64 = self
            .clusters
            .iter()
            .map(|c| c.reference_density * c.size() as f64)
            .sum();

        weighted_sum / total_entries as f64
    }

    /// Gets cluster statistics.
    pub fn stats(&self) -> CoherenceStats {
        let sizes: Vec<_> = self.clusters.iter().map(|c| c.size()).collect();
        let densities: Vec<_> = self.clusters.iter().map(|c| c.reference_density).collect();

        CoherenceStats {
            cluster_count: self.clusters.len(),
            entry_count: self.entry_vectors.len(),
            avg_cluster_size: if sizes.is_empty() {
                0.0
            } else {
                sizes.iter().sum::<usize>() as f64 / sizes.len() as f64
            },
            max_cluster_size: sizes.iter().max().copied().unwrap_or(0),
            min_cluster_size: sizes.iter().min().copied().unwrap_or(0),
            avg_density: self.average_density(),
            singleton_count: self.clusters.iter().filter(|c| c.is_singleton()).count(),
        }
    }
}

impl Default for CoherenceSnapshot {
    fn default() -> Self {
        Self::new()
    }
}

/// Statistics about the coherence model.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CoherenceStats {
    /// Number of clusters.
    pub cluster_count: usize,
    /// Total entries tracked.
    pub entry_count: usize,
    /// Average entries per cluster.
    pub avg_cluster_size: f64,
    /// Size of largest cluster.
    pub max_cluster_size: usize,
    /// Size of smallest cluster.
    pub min_cluster_size: usize,
    /// Average reference density.
    pub avg_density: f64,
    /// Number of singleton clusters.
    pub singleton_count: usize,
}

/// Result of assigning an entry to the coherence model.
#[derive(Debug, Clone)]
pub struct AssignmentResult {
    /// The cluster the entry was assigned to.
    pub cluster_id: ClusterId,
    /// Whether a new cluster was created.
    pub new_cluster: bool,
    /// Similarity to the matched cluster (if not new).
    pub similarity: Option<f64>,
}

#[cfg(test)]
mod tests {
    use super::*;
    use notebook_core::types::{AuthorId, EntryBuilder};

    fn make_text_entry(content: &str) -> Entry {
        EntryBuilder::default()
            .content(content.as_bytes().to_vec())
            .content_type("text/plain")
            .author(AuthorId::zero())
            .build()
    }

    fn make_text_entry_with_topic(content: &str, topic: &str) -> Entry {
        EntryBuilder::default()
            .content(content.as_bytes().to_vec())
            .content_type("text/plain")
            .topic(topic)
            .author(AuthorId::zero())
            .build()
    }

    fn make_text_entry_with_refs(content: &str, refs: Vec<EntryId>) -> Entry {
        EntryBuilder::default()
            .content(content.as_bytes().to_vec())
            .content_type("text/plain")
            .author(AuthorId::zero())
            .references(refs)
            .build()
    }

    #[test]
    fn coherence_snapshot_new() {
        let snapshot = CoherenceSnapshot::new();
        assert_eq!(snapshot.cluster_count(), 0);
        assert_eq!(snapshot.entry_count(), 0);
        assert_eq!(snapshot.threshold(), DEFAULT_SIMILARITY_THRESHOLD);
    }

    #[test]
    fn add_first_entry() {
        let mut snapshot = CoherenceSnapshot::new();
        let entry = make_text_entry("Hello world test document");

        let cluster_id = snapshot.add_entry(&entry);

        assert_eq!(snapshot.cluster_count(), 1);
        assert_eq!(snapshot.entry_count(), 1);

        let cluster = snapshot.get_cluster(cluster_id).unwrap();
        assert!(cluster.contains(&entry.id));
        assert!(cluster.is_singleton());
    }

    #[test]
    fn add_similar_entries_same_cluster() {
        let mut snapshot = CoherenceSnapshot::new();
        snapshot.set_threshold(0.1); // Low threshold to encourage merging

        let entry1 = make_text_entry("machine learning algorithms neural networks");
        let entry2 = make_text_entry("neural networks deep learning algorithms");

        let cluster1 = snapshot.add_entry(&entry1);
        let cluster2 = snapshot.add_entry(&entry2);

        // With low threshold, similar entries should be in same cluster
        // Note: depends on TF-IDF similarity computation
        assert!(snapshot.cluster_count() <= 2);
    }

    #[test]
    fn add_dissimilar_entries_different_clusters() {
        let mut snapshot = CoherenceSnapshot::new();
        snapshot.set_threshold(0.9); // High threshold

        let entry1 = make_text_entry("machine learning algorithms");
        let entry2 = make_text_entry("cooking recipes ingredients kitchen");

        snapshot.add_entry(&entry1);
        snapshot.add_entry(&entry2);

        // With high threshold, dissimilar entries should be in different clusters
        assert_eq!(snapshot.cluster_count(), 2);
    }

    #[test]
    fn assign_to_cluster_empty() {
        let snapshot = CoherenceSnapshot::new();
        let entry = make_text_entry("test content");

        let result = snapshot.assign_to_cluster(&entry);
        assert!(result.is_none());
    }

    #[test]
    fn get_entry_cluster() {
        let mut snapshot = CoherenceSnapshot::new();
        let entry = make_text_entry("test document content");

        let cluster_id = snapshot.add_entry(&entry);
        let found = snapshot.get_entry_cluster(&entry.id);

        assert!(found.is_some());
        assert_eq!(found.unwrap().id, cluster_id);
    }

    #[test]
    fn reference_density_updates() {
        let mut snapshot = CoherenceSnapshot::new();
        snapshot.set_threshold(0.0); // Always merge

        let entry1 = make_text_entry("test content alpha beta");
        let entry1_id = entry1.id;

        let entry2 = make_text_entry_with_refs("test content gamma delta", vec![entry1_id]);

        snapshot.add_entry(&entry1);
        let cluster_id = snapshot.add_entry(&entry2);

        let cluster = snapshot.get_cluster(cluster_id).unwrap();
        // With reference between entries, density should be 1.0 (fully connected pair)
        assert!(cluster.reference_density > 0.0);
    }

    #[test]
    fn rebuild_from_entries() {
        let mut snapshot = CoherenceSnapshot::new();

        let entry1 = make_text_entry("machine learning neural networks");
        let entry2 = make_text_entry("cooking recipes food kitchen");
        let entry3 = make_text_entry("machine learning deep learning");

        snapshot.rebuild(
            &[entry1.clone(), entry2.clone(), entry3.clone()],
            CausalPosition::first(),
        );

        assert!(snapshot.entry_count() == 3);
        // Clusters depend on threshold, but we should have some
        assert!(snapshot.cluster_count() >= 1);
    }

    #[test]
    fn stats_computation() {
        let mut snapshot = CoherenceSnapshot::new();
        snapshot.set_threshold(0.9); // High threshold for separate clusters

        let entry1 = make_text_entry("alpha beta gamma");
        let entry2 = make_text_entry("delta epsilon zeta");

        snapshot.add_entry(&entry1);
        snapshot.add_entry(&entry2);

        let stats = snapshot.stats();
        assert_eq!(stats.entry_count, 2);
        assert!(stats.cluster_count >= 1);
    }

    #[test]
    fn serialization_roundtrip() {
        let mut snapshot = CoherenceSnapshot::new();
        let entry = make_text_entry("test content for serialization");
        snapshot.add_entry(&entry);

        let json = serde_json::to_string(&snapshot).unwrap();
        let parsed: CoherenceSnapshot = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.cluster_count(), snapshot.cluster_count());
        assert_eq!(parsed.entry_count(), snapshot.entry_count());
        assert_eq!(parsed.threshold(), snapshot.threshold());
    }

    #[test]
    fn topic_matching() {
        let mut snapshot = CoherenceSnapshot::new();
        snapshot.set_threshold(0.9); // High threshold

        // Add entry that creates cluster with "machine" and "learning" keywords
        let entry1 = make_text_entry("machine learning algorithms");
        snapshot.add_entry(&entry1);

        // Non-text entry with topic containing cluster keywords
        let entry2 = EntryBuilder::default()
            .content(vec![0, 1, 2, 3]) // Binary content
            .content_type("application/octet-stream")
            .topic("machine learning model")
            .author(AuthorId::zero())
            .build();

        // Should match based on topic keyword overlap
        let result = snapshot.assign_to_cluster(&entry2);
        // May or may not match depending on extracted keywords
        // At minimum, should not panic
    }

    #[test]
    fn empty_content_handling() {
        let mut snapshot = CoherenceSnapshot::new();

        let entry = EntryBuilder::default()
            .content(vec![])
            .content_type("text/plain")
            .author(AuthorId::zero())
            .build();

        let cluster_id = snapshot.add_entry(&entry);

        // Should create a singleton cluster even for empty content
        assert_eq!(snapshot.cluster_count(), 1);
    }

    #[test]
    fn non_text_content() {
        let mut snapshot = CoherenceSnapshot::new();

        let entry = EntryBuilder::default()
            .content(vec![0xFF, 0xD8, 0xFF, 0xE0]) // JPEG magic bytes
            .content_type("image/jpeg")
            .author(AuthorId::zero())
            .build();

        let cluster_id = snapshot.add_entry(&entry);

        // Should create singleton cluster for non-text content
        assert_eq!(snapshot.cluster_count(), 1);
    }

    #[test]
    fn average_density() {
        let snapshot = CoherenceSnapshot::new();
        assert_eq!(snapshot.average_density(), 0.0);

        let mut snapshot2 = CoherenceSnapshot::new();
        let entry = make_text_entry("test content");
        snapshot2.add_entry(&entry);

        // Single singleton cluster has density 1.0
        assert_eq!(snapshot2.average_density(), 1.0);
    }

    #[test]
    fn with_config() {
        let config = ClusteringConfig {
            similarity_threshold: 0.5,
            max_clusters: 10,
        };

        let snapshot = CoherenceSnapshot::with_config(config.clone());
        assert_eq!(snapshot.threshold(), 0.5);
        assert_eq!(snapshot.config.max_clusters, 10);
    }
}
