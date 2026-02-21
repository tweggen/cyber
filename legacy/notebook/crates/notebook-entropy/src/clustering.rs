//! Topic clustering for notebook entries.
//!
//! This module implements agglomerative clustering based on keyword similarity.
//! Entries are grouped into clusters by their TF-IDF vector similarity, with
//! configurable similarity thresholds.
//!
//! The clustering approach:
//! 1. Start with each entry as a singleton cluster
//! 2. Iteratively merge the two most similar clusters
//! 3. Stop when no pair exceeds the similarity threshold

use crate::tfidf::{TfIdfVector, merge_vectors};
use notebook_core::types::EntryId;
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, HashSet};
use std::fmt;

/// Unique identifier for a cluster.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ClusterId(pub u64);

impl ClusterId {
    /// Creates a new ClusterId from a raw value.
    pub const fn new(id: u64) -> Self {
        Self(id)
    }
}

impl fmt::Display for ClusterId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "cluster-{}", self.0)
    }
}

/// Default similarity threshold for clustering.
/// Pairs with similarity below this threshold will not be merged.
pub const DEFAULT_SIMILARITY_THRESHOLD: f64 = 0.3;

/// Number of top keywords to extract per cluster.
const TOP_KEYWORDS_COUNT: usize = 5;

/// A cluster of related entries.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Cluster {
    /// Unique identifier for this cluster.
    pub id: ClusterId,

    /// Top keywords characterizing this cluster, sorted by importance.
    pub topic_keywords: Vec<String>,

    /// Entry IDs belonging to this cluster.
    pub entry_ids: Vec<EntryId>,

    /// Reference density within the cluster (edges / possible_edges).
    /// 1.0 for singleton clusters, 0.0-1.0 for larger clusters.
    pub reference_density: f64,
}

impl Cluster {
    /// Creates a new singleton cluster containing one entry.
    pub fn singleton(id: ClusterId, entry_id: EntryId, keywords: Vec<String>) -> Self {
        Self {
            id,
            topic_keywords: keywords,
            entry_ids: vec![entry_id],
            reference_density: 1.0, // Singleton has perfect density by convention
        }
    }

    /// Returns the number of entries in this cluster.
    pub fn size(&self) -> usize {
        self.entry_ids.len()
    }

    /// Checks if the cluster is a singleton (single entry).
    pub fn is_singleton(&self) -> bool {
        self.entry_ids.len() == 1
    }

    /// Checks if the cluster contains a specific entry.
    pub fn contains(&self, entry_id: &EntryId) -> bool {
        self.entry_ids.contains(entry_id)
    }
}

/// Configuration for the clustering algorithm.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ClusteringConfig {
    /// Minimum cosine similarity for merging clusters.
    pub similarity_threshold: f64,

    /// Maximum number of clusters (0 = unlimited).
    pub max_clusters: usize,
}

impl Default for ClusteringConfig {
    fn default() -> Self {
        Self {
            similarity_threshold: DEFAULT_SIMILARITY_THRESHOLD,
            max_clusters: 0,
        }
    }
}

/// Intermediate state during clustering.
struct ClusterState {
    /// Current clusters indexed by ID.
    clusters: HashMap<ClusterId, Cluster>,

    /// TF-IDF vectors for each cluster (merged from member entries).
    cluster_vectors: HashMap<ClusterId, TfIdfVector>,

    /// Next cluster ID to assign.
    next_id: u64,
}

impl ClusterState {
    fn new() -> Self {
        Self {
            clusters: HashMap::new(),
            cluster_vectors: HashMap::new(),
            next_id: 0,
        }
    }

    fn allocate_id(&mut self) -> ClusterId {
        let id = ClusterId::new(self.next_id);
        self.next_id += 1;
        id
    }

    fn add_singleton(&mut self, entry_id: EntryId, vector: TfIdfVector) -> ClusterId {
        let id = self.allocate_id();
        let keywords = vector.top_terms(TOP_KEYWORDS_COUNT);
        let cluster = Cluster::singleton(id, entry_id, keywords);
        self.clusters.insert(id, cluster);
        self.cluster_vectors.insert(id, vector);
        id
    }

    fn merge(&mut self, id1: ClusterId, id2: ClusterId, references: &ReferenceGraph) -> ClusterId {
        let c1 = self.clusters.remove(&id1).expect("cluster 1 exists");
        let c2 = self.clusters.remove(&id2).expect("cluster 2 exists");
        let v1 = self.cluster_vectors.remove(&id1).expect("vector 1 exists");
        let v2 = self.cluster_vectors.remove(&id2).expect("vector 2 exists");

        // Merge entry IDs
        let mut entry_ids = c1.entry_ids;
        entry_ids.extend(c2.entry_ids);

        // Merge TF-IDF vectors
        let merged_vector = merge_vectors(&[&v1, &v2]);
        let keywords = merged_vector.top_terms(TOP_KEYWORDS_COUNT);

        // Calculate reference density for merged cluster
        let density = calculate_reference_density(&entry_ids, references);

        let new_id = self.allocate_id();
        let cluster = Cluster {
            id: new_id,
            topic_keywords: keywords,
            entry_ids,
            reference_density: density,
        };

        self.clusters.insert(new_id, cluster);
        self.cluster_vectors.insert(new_id, merged_vector);
        new_id
    }

    fn find_best_merge(&self, threshold: f64) -> Option<(ClusterId, ClusterId, f64)> {
        let ids: Vec<_> = self.clusters.keys().copied().collect();
        let mut best: Option<(ClusterId, ClusterId, f64)> = None;

        for i in 0..ids.len() {
            for j in (i + 1)..ids.len() {
                let v1 = &self.cluster_vectors[&ids[i]];
                let v2 = &self.cluster_vectors[&ids[j]];
                let sim = v1.cosine_similarity(v2);

                if sim >= threshold && (best.is_none() || sim > best.as_ref().unwrap().2) {
                    best = Some((ids[i], ids[j], sim));
                }
            }
        }

        best
    }
}

/// Graph of references between entries.
#[derive(Debug, Clone, Default)]
pub struct ReferenceGraph {
    /// For each entry, the set of entries it references.
    edges: HashMap<EntryId, HashSet<EntryId>>,
}

impl ReferenceGraph {
    /// Creates a new empty reference graph.
    pub fn new() -> Self {
        Self::default()
    }

    /// Adds a reference from one entry to another.
    pub fn add_reference(&mut self, from: EntryId, to: EntryId) {
        self.edges.entry(from).or_default().insert(to);
    }

    /// Adds all references for an entry.
    pub fn add_entry_references(&mut self, entry_id: EntryId, references: &[EntryId]) {
        for ref_id in references {
            self.add_reference(entry_id, *ref_id);
        }
    }

    /// Checks if there is a reference between two entries (in either direction).
    pub fn has_edge(&self, a: &EntryId, b: &EntryId) -> bool {
        self.edges.get(a).is_some_and(|refs| refs.contains(b))
            || self.edges.get(b).is_some_and(|refs| refs.contains(a))
    }

    /// Counts edges within a set of entries.
    pub fn count_internal_edges(&self, entries: &[EntryId]) -> usize {
        let _entry_set: HashSet<_> = entries.iter().copied().collect();
        let mut count = 0;

        for i in 0..entries.len() {
            for j in (i + 1)..entries.len() {
                if self.has_edge(&entries[i], &entries[j]) {
                    count += 1;
                }
            }
        }

        count
    }
}

/// Calculates reference density for a set of entries.
///
/// Density = internal_edges / possible_edges
/// where possible_edges = n * (n-1) / 2 for n entries.
///
/// Returns 1.0 for singleton sets (by convention).
pub fn calculate_reference_density(entries: &[EntryId], references: &ReferenceGraph) -> f64 {
    let n = entries.len();
    if n <= 1 {
        return 1.0;
    }

    let possible_edges = n * (n - 1) / 2;
    let actual_edges = references.count_internal_edges(entries);

    actual_edges as f64 / possible_edges as f64
}

/// Performs agglomerative clustering on entries.
///
/// # Arguments
///
/// * `entries` - Pairs of (EntryId, TfIdfVector) for each entry
/// * `references` - Reference graph for density calculation
/// * `config` - Clustering configuration
///
/// # Returns
///
/// A vector of clusters
pub fn cluster_entries(
    entries: Vec<(EntryId, TfIdfVector)>,
    references: &ReferenceGraph,
    config: &ClusteringConfig,
) -> Vec<Cluster> {
    if entries.is_empty() {
        return Vec::new();
    }

    let mut state = ClusterState::new();

    // Initialize with singleton clusters
    for (entry_id, vector) in entries {
        state.add_singleton(entry_id, vector);
    }

    // Agglomerative merging
    loop {
        // Check max clusters limit
        if config.max_clusters > 0 && state.clusters.len() <= config.max_clusters {
            break;
        }

        // Find best merge candidate
        match state.find_best_merge(config.similarity_threshold) {
            Some((id1, id2, _sim)) => {
                state.merge(id1, id2, references);
            }
            None => break, // No more merges above threshold
        }
    }

    state.clusters.into_values().collect()
}

/// Finds the best matching cluster for a new entry.
///
/// # Arguments
///
/// * `vector` - TF-IDF vector for the new entry
/// * `clusters` - Existing clusters with their vectors
/// * `threshold` - Minimum similarity to match
///
/// # Returns
///
/// The best matching cluster ID and similarity, or None if no match above threshold
pub fn find_best_cluster(
    vector: &TfIdfVector,
    clusters: &[(ClusterId, TfIdfVector)],
    threshold: f64,
) -> Option<(ClusterId, f64)> {
    clusters
        .iter()
        .map(|(id, cluster_vec)| (*id, vector.cosine_similarity(cluster_vec)))
        .filter(|(_, sim)| *sim >= threshold)
        .max_by(|a, b| a.1.partial_cmp(&b.1).unwrap_or(std::cmp::Ordering::Equal))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;

    fn make_vector(terms: &[(&str, f64)]) -> TfIdfVector {
        let weights = terms.iter().map(|(t, w)| (t.to_string(), *w)).collect();
        TfIdfVector { weights }
    }

    #[test]
    fn cluster_id_display() {
        let id = ClusterId::new(42);
        assert_eq!(id.to_string(), "cluster-42");
    }

    #[test]
    fn cluster_singleton() {
        let entry_id = EntryId::new();
        let cluster = Cluster::singleton(ClusterId::new(1), entry_id, vec!["test".into()]);

        assert!(cluster.is_singleton());
        assert_eq!(cluster.size(), 1);
        assert!(cluster.contains(&entry_id));
        assert_eq!(cluster.reference_density, 1.0);
    }

    #[test]
    fn reference_graph_basic() {
        let mut graph = ReferenceGraph::new();
        let e1 = EntryId::new();
        let e2 = EntryId::new();
        let e3 = EntryId::new();

        graph.add_reference(e1, e2);

        assert!(graph.has_edge(&e1, &e2));
        assert!(graph.has_edge(&e2, &e1)); // Symmetric check
        assert!(!graph.has_edge(&e1, &e3));
    }

    #[test]
    fn reference_density_singleton() {
        let graph = ReferenceGraph::new();
        let entries = vec![EntryId::new()];
        let density = calculate_reference_density(&entries, &graph);
        assert_eq!(density, 1.0);
    }

    #[test]
    fn reference_density_empty() {
        let graph = ReferenceGraph::new();
        let entries: Vec<EntryId> = vec![];
        let density = calculate_reference_density(&entries, &graph);
        assert_eq!(density, 1.0);
    }

    #[test]
    fn reference_density_full() {
        let mut graph = ReferenceGraph::new();
        let e1 = EntryId::new();
        let e2 = EntryId::new();
        let e3 = EntryId::new();

        // Fully connected: e1-e2, e1-e3, e2-e3
        graph.add_reference(e1, e2);
        graph.add_reference(e1, e3);
        graph.add_reference(e2, e3);

        let entries = vec![e1, e2, e3];
        let density = calculate_reference_density(&entries, &graph);
        assert_eq!(density, 1.0);
    }

    #[test]
    fn reference_density_partial() {
        let mut graph = ReferenceGraph::new();
        let e1 = EntryId::new();
        let e2 = EntryId::new();
        let e3 = EntryId::new();

        // Only one edge: e1-e2
        graph.add_reference(e1, e2);

        let entries = vec![e1, e2, e3];
        // Possible edges: 3, actual edges: 1
        let density = calculate_reference_density(&entries, &graph);
        assert!((density - 1.0 / 3.0).abs() < 0.001);
    }

    #[test]
    fn cluster_entries_empty() {
        let config = ClusteringConfig::default();
        let references = ReferenceGraph::new();
        let clusters = cluster_entries(vec![], &references, &config);
        assert!(clusters.is_empty());
    }

    #[test]
    fn cluster_entries_singleton() {
        let config = ClusteringConfig::default();
        let references = ReferenceGraph::new();
        let entry_id = EntryId::new();
        let vector = make_vector(&[("test", 1.0)]);

        let clusters = cluster_entries(vec![(entry_id, vector)], &references, &config);

        assert_eq!(clusters.len(), 1);
        assert!(clusters[0].contains(&entry_id));
    }

    #[test]
    fn cluster_entries_similar_merge() {
        let config = ClusteringConfig {
            similarity_threshold: 0.5,
            max_clusters: 0,
        };
        let references = ReferenceGraph::new();

        let e1 = EntryId::new();
        let e2 = EntryId::new();

        // Very similar vectors
        let v1 = make_vector(&[("cat", 1.0), ("dog", 0.5)]);
        let v2 = make_vector(&[("cat", 0.8), ("dog", 0.6)]);

        let clusters = cluster_entries(vec![(e1, v1), (e2, v2)], &references, &config);

        // Should merge into single cluster due to high similarity
        assert_eq!(clusters.len(), 1);
        assert!(clusters[0].contains(&e1));
        assert!(clusters[0].contains(&e2));
    }

    #[test]
    fn cluster_entries_dissimilar_separate() {
        let config = ClusteringConfig {
            similarity_threshold: 0.5,
            max_clusters: 0,
        };
        let references = ReferenceGraph::new();

        let e1 = EntryId::new();
        let e2 = EntryId::new();

        // Orthogonal vectors
        let v1 = make_vector(&[("cat", 1.0)]);
        let v2 = make_vector(&[("dog", 1.0)]);

        let clusters = cluster_entries(vec![(e1, v1), (e2, v2)], &references, &config);

        // Should remain separate due to zero similarity
        assert_eq!(clusters.len(), 2);
    }

    #[test]
    fn find_best_cluster_match() {
        let vector = make_vector(&[("cat", 1.0), ("dog", 0.5)]);
        let c1_vec = make_vector(&[("cat", 0.8), ("dog", 0.6)]);
        let c2_vec = make_vector(&[("bird", 1.0)]);

        let clusters = vec![(ClusterId::new(1), c1_vec), (ClusterId::new(2), c2_vec)];

        let result = find_best_cluster(&vector, &clusters, 0.5);
        assert!(result.is_some());
        assert_eq!(result.unwrap().0, ClusterId::new(1));
    }

    #[test]
    fn find_best_cluster_no_match() {
        let vector = make_vector(&[("fish", 1.0)]);
        let c1_vec = make_vector(&[("cat", 1.0)]);
        let c2_vec = make_vector(&[("dog", 1.0)]);

        let clusters = vec![(ClusterId::new(1), c1_vec), (ClusterId::new(2), c2_vec)];

        let result = find_best_cluster(&vector, &clusters, 0.5);
        assert!(result.is_none());
    }

    #[test]
    fn cluster_serialization() {
        let cluster = Cluster {
            id: ClusterId::new(42),
            topic_keywords: vec!["test".into(), "demo".into()],
            entry_ids: vec![EntryId::new()],
            reference_density: 0.75,
        };

        let json = serde_json::to_string(&cluster).unwrap();
        let parsed: Cluster = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.id, cluster.id);
        assert_eq!(parsed.topic_keywords, cluster.topic_keywords);
        assert_eq!(parsed.reference_density, cluster.reference_density);
    }

    #[test]
    fn clustering_config_default() {
        let config = ClusteringConfig::default();
        assert_eq!(config.similarity_threshold, DEFAULT_SIMILARITY_THRESHOLD);
        assert_eq!(config.max_clusters, 0);
    }
}
