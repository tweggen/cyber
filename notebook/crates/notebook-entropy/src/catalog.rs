//! Catalog generation for dense summaries of notebook contents.
//!
//! This module generates a catalog from a CoherenceSnapshot, producing
//! dense summaries that fit within a token budget. The catalog is used
//! by the BROWSE endpoint to provide agents with a quick overview of
//! notebook contents.
//!
//! ## Algorithm
//!
//! 1. Map each cluster to a ClusterSummary with topic, summary, and metrics
//! 2. Sort by cumulative_cost (most significant first), then stability
//! 3. Truncate to fit the token budget
//! 4. Return the Catalog with overall entropy metrics
//!
//! ## Token Budget
//!
//! Each ClusterSummary is estimated at ~75 tokens. The default budget
//! is 4000 tokens, allowing approximately 53 cluster summaries.
//!
//! Owned by: agent-catalog (Task 3-1)

use crate::clustering::Cluster;
use crate::coherence::CoherenceSnapshot;
use notebook_core::types::{CausalPosition, Entry, EntryId};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

/// Default maximum tokens for catalog generation.
pub const DEFAULT_MAX_TOKENS: usize = 4000;

/// Estimated tokens per cluster summary.
const TOKENS_PER_SUMMARY: usize = 75;

/// Maximum characters for the summary text.
const MAX_SUMMARY_CHARS: usize = 150;

/// Maximum representative entry IDs per cluster.
const MAX_REPRESENTATIVE_ENTRIES: usize = 3;

/// Maximum keywords to include in topic.
const MAX_TOPIC_KEYWORDS: usize = 3;

/// A dense catalog of notebook contents.
///
/// The catalog provides a quick overview of notebook structure, showing
/// cluster summaries ordered by significance. It's designed to fit within
/// an attention budget (token limit) for efficient agent consumption.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Catalog {
    /// Cluster summaries ordered by significance.
    pub clusters: Vec<ClusterSummary>,

    /// Overall entropy measure for the notebook.
    /// Computed as the sum of all cumulative costs.
    pub notebook_entropy: f64,

    /// Total number of entries in the notebook.
    pub total_entries: u32,

    /// Causal position when this catalog was generated.
    pub generated_at: CausalPosition,
}

/// Summary of a single cluster for the catalog.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ClusterSummary {
    /// Topic extracted from cluster keywords.
    pub topic: String,

    /// One-line extractive summary from cluster content.
    pub summary: String,

    /// Number of entries in this cluster.
    pub entry_count: u32,

    /// Total integration cost caused by entries in this cluster.
    pub cumulative_cost: f64,

    /// Entries since last cluster modification (higher = more stable).
    pub stability: u64,

    /// Representative entry IDs from this cluster.
    pub representative_entry_ids: Vec<EntryId>,
}

/// Generator for creating catalogs from coherence snapshots.
///
/// # Example
///
/// ```rust,ignore
/// use notebook_entropy::catalog::CatalogGenerator;
/// use notebook_entropy::coherence::CoherenceSnapshot;
///
/// let generator = CatalogGenerator::new();
/// let catalog = generator.generate(&snapshot, &entries, 4000);
/// ```
pub struct CatalogGenerator {
    /// Token budget for generated catalogs.
    max_tokens: usize,
}

impl CatalogGenerator {
    /// Creates a new CatalogGenerator with the default token budget.
    pub fn new() -> Self {
        Self {
            max_tokens: DEFAULT_MAX_TOKENS,
        }
    }

    /// Creates a CatalogGenerator with a custom token budget.
    pub fn with_max_tokens(max_tokens: usize) -> Self {
        Self { max_tokens }
    }

    /// Sets the maximum token budget.
    pub fn set_max_tokens(&mut self, max_tokens: usize) {
        self.max_tokens = max_tokens;
    }

    /// Returns the current token budget.
    pub fn max_tokens(&self) -> usize {
        self.max_tokens
    }

    /// Generates a catalog from a coherence snapshot and entry list.
    ///
    /// # Arguments
    ///
    /// * `snapshot` - The coherence snapshot containing cluster information
    /// * `entries` - All entries in the notebook (for content extraction)
    /// * `max_tokens` - Maximum tokens for the output (overrides default)
    ///
    /// # Returns
    ///
    /// A Catalog with cluster summaries truncated to fit the token budget.
    pub fn generate(
        &self,
        snapshot: &CoherenceSnapshot,
        entries: &[Entry],
        max_tokens: Option<usize>,
    ) -> Catalog {
        let budget = max_tokens.unwrap_or(self.max_tokens);
        let max_clusters = budget / TOKENS_PER_SUMMARY;

        // Build entry lookup for efficient access
        let entry_map: HashMap<EntryId, &Entry> = entries.iter().map(|e| (e.id, e)).collect();

        // Generate summaries for each cluster
        let mut summaries: Vec<ClusterSummary> = snapshot
            .clusters
            .iter()
            .map(|cluster| self.summarize_cluster(cluster, &entry_map, snapshot))
            .collect();

        // Sort by cumulative_cost DESC, then stability DESC
        summaries.sort_by(|a, b| {
            b.cumulative_cost
                .partial_cmp(&a.cumulative_cost)
                .unwrap_or(std::cmp::Ordering::Equal)
                .then_with(|| b.stability.cmp(&a.stability))
        });

        // Truncate to fit token budget
        summaries.truncate(max_clusters);

        // Compute overall notebook entropy
        let notebook_entropy: f64 = summaries.iter().map(|s| s.cumulative_cost).sum();

        Catalog {
            clusters: summaries,
            notebook_entropy,
            total_entries: snapshot.entry_count() as u32,
            generated_at: snapshot.timestamp,
        }
    }

    /// Summarizes a single cluster.
    fn summarize_cluster(
        &self,
        cluster: &Cluster,
        entry_map: &HashMap<EntryId, &Entry>,
        snapshot: &CoherenceSnapshot,
    ) -> ClusterSummary {
        // Extract topic from keywords
        let topic = cluster
            .topic_keywords
            .iter()
            .take(MAX_TOPIC_KEYWORDS)
            .cloned()
            .collect::<Vec<_>>()
            .join(", ");

        // Extract summary from first text entry
        let summary = self.extract_summary(cluster, entry_map);

        // Compute cumulative cost from all entries in cluster
        let cumulative_cost = self.compute_cumulative_cost(cluster, entry_map);

        // Compute stability (entries since last modification)
        let stability = self.compute_stability(cluster, entry_map, snapshot);

        // Get representative entry IDs
        let representative_entry_ids: Vec<EntryId> = cluster
            .entry_ids
            .iter()
            .take(MAX_REPRESENTATIVE_ENTRIES)
            .copied()
            .collect();

        ClusterSummary {
            topic,
            summary,
            entry_count: cluster.size() as u32,
            cumulative_cost,
            stability,
            representative_entry_ids,
        }
    }

    /// Extracts a summary from the first text entry in the cluster.
    fn extract_summary(&self, cluster: &Cluster, entry_map: &HashMap<EntryId, &Entry>) -> String {
        // Find first text entry
        for entry_id in &cluster.entry_ids {
            if let Some(entry) = entry_map.get(entry_id)
                && entry.content_type.starts_with("text/")
            {
                let text = String::from_utf8_lossy(&entry.content);
                return self.extract_first_sentence(&text);
            }
        }

        // Fallback for non-text clusters
        if cluster.topic_keywords.is_empty() {
            format!("[{} entries]", cluster.size())
        } else {
            format!(
                "[{} entries about {}]",
                cluster.size(),
                cluster.topic_keywords.first().unwrap_or(&String::new())
            )
        }
    }

    /// Extracts the first sentence or truncated content.
    fn extract_first_sentence(&self, text: &str) -> String {
        let text = text.trim();

        // Try to find first sentence ending
        let end_markers = [". ", ".\n", "! ", "!\n", "? ", "?\n"];
        let mut end_pos = MAX_SUMMARY_CHARS.min(text.len());

        for marker in &end_markers {
            if let Some(pos) = text.find(marker)
                && pos < end_pos
                && pos > 0
            {
                end_pos = pos + 1; // Include the punctuation
            }
        }

        // Extract and clean up
        let mut summary: String = text.chars().take(end_pos).collect();

        // If we truncated mid-sentence, add ellipsis
        if end_pos < text.len()
            && !summary.ends_with('.')
            && !summary.ends_with('!')
            && !summary.ends_with('?')
        {
            // Try to truncate at word boundary
            if let Some(last_space) = summary.rfind(' ')
                && last_space > summary.len() / 2
            {
                summary.truncate(last_space);
            }
            summary.push_str("...");
        }

        summary
    }

    /// Computes cumulative integration cost for a cluster.
    fn compute_cumulative_cost(
        &self,
        cluster: &Cluster,
        entry_map: &HashMap<EntryId, &Entry>,
    ) -> f64 {
        cluster
            .entry_ids
            .iter()
            .filter_map(|id| entry_map.get(id))
            .map(|entry| entry.integration_cost.catalog_shift)
            .sum()
    }

    /// Computes stability for a cluster.
    ///
    /// Stability is the number of entries since the cluster last changed.
    /// Approximated as: max_sequence - max(entry sequences in cluster)
    fn compute_stability(
        &self,
        cluster: &Cluster,
        entry_map: &HashMap<EntryId, &Entry>,
        snapshot: &CoherenceSnapshot,
    ) -> u64 {
        let max_entry_sequence = cluster
            .entry_ids
            .iter()
            .filter_map(|id| entry_map.get(id))
            .map(|entry| entry.causal_position.sequence)
            .max()
            .unwrap_or(0);

        let snapshot_sequence = snapshot.timestamp.sequence;

        // Stability = entries since last modification to this cluster
        snapshot_sequence.saturating_sub(max_entry_sequence)
    }
}

impl Default for CatalogGenerator {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::clustering::ClusterId;
    use notebook_core::types::{AuthorId, EntryBuilder, IntegrationCost};

    fn make_text_entry(content: &str, sequence: u64) -> Entry {
        EntryBuilder::default()
            .content(content.as_bytes().to_vec())
            .content_type("text/plain")
            .author(AuthorId::zero())
            .causal_position(CausalPosition {
                sequence,
                ..Default::default()
            })
            .integration_cost(IntegrationCost {
                catalog_shift: 0.5,
                ..Default::default()
            })
            .build()
    }

    fn make_cluster(id: u64, keywords: &[&str], entry_ids: Vec<EntryId>) -> Cluster {
        Cluster {
            id: ClusterId::new(id),
            topic_keywords: keywords.iter().map(|s| s.to_string()).collect(),
            entry_ids,
            reference_density: 1.0,
        }
    }

    #[test]
    fn generator_new() {
        let generator = CatalogGenerator::new();
        assert_eq!(generator.max_tokens(), DEFAULT_MAX_TOKENS);
    }

    #[test]
    fn generator_with_max_tokens() {
        let generator = CatalogGenerator::with_max_tokens(2000);
        assert_eq!(generator.max_tokens(), 2000);
    }

    #[test]
    fn generator_set_max_tokens() {
        let mut generator = CatalogGenerator::new();
        generator.set_max_tokens(1000);
        assert_eq!(generator.max_tokens(), 1000);
    }

    #[test]
    fn extract_first_sentence_simple() {
        let generator = CatalogGenerator::new();
        let text = "This is the first sentence. This is the second.";
        let result = generator.extract_first_sentence(text);
        assert_eq!(result, "This is the first sentence.");
    }

    #[test]
    fn extract_first_sentence_truncate() {
        let generator = CatalogGenerator::new();
        let text = "This is a very long text without any sentence ending that goes on and on and on and on and eventually needs to be truncated at some point for readability";
        let result = generator.extract_first_sentence(text);
        assert!(result.len() <= MAX_SUMMARY_CHARS + 3); // +3 for "..."
        assert!(result.ends_with("..."));
    }

    #[test]
    fn extract_first_sentence_short() {
        let generator = CatalogGenerator::new();
        let text = "Short text.";
        let result = generator.extract_first_sentence(text);
        assert_eq!(result, "Short text.");
    }

    #[test]
    fn generate_empty_snapshot() {
        let generator = CatalogGenerator::new();
        let snapshot = CoherenceSnapshot::new();
        let entries: Vec<Entry> = vec![];

        let catalog = generator.generate(&snapshot, &entries, None);

        assert!(catalog.clusters.is_empty());
        assert_eq!(catalog.total_entries, 0);
        assert_eq!(catalog.notebook_entropy, 0.0);
    }

    #[test]
    fn generate_single_cluster() {
        let generator = CatalogGenerator::new();

        // Create entry and cluster
        let entry = make_text_entry("Machine learning is fascinating. More content here.", 1);
        let entry_id = entry.id;

        let cluster = make_cluster(0, &["machine", "learning"], vec![entry_id]);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster);

        let catalog = generator.generate(&snapshot, &[entry], None);

        assert_eq!(catalog.clusters.len(), 1);
        assert_eq!(catalog.clusters[0].topic, "machine, learning");
        assert!(catalog.clusters[0].summary.contains("Machine learning"));
        assert_eq!(catalog.clusters[0].entry_count, 1);
    }

    #[test]
    fn generate_ordering_by_cost() {
        let generator = CatalogGenerator::new();

        // Create entries with different costs
        let mut entry1 = make_text_entry("Low cost entry", 1);
        entry1.integration_cost.catalog_shift = 0.1;
        let entry1_id = entry1.id;

        let mut entry2 = make_text_entry("High cost entry", 2);
        entry2.integration_cost.catalog_shift = 0.9;
        let entry2_id = entry2.id;

        let cluster1 = make_cluster(0, &["low"], vec![entry1_id]);
        let cluster2 = make_cluster(1, &["high"], vec![entry2_id]);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster1);
        snapshot.clusters.push(cluster2);

        let catalog = generator.generate(&snapshot, &[entry1, entry2], None);

        // High cost cluster should come first
        assert_eq!(catalog.clusters.len(), 2);
        assert_eq!(catalog.clusters[0].topic, "high");
        assert_eq!(catalog.clusters[1].topic, "low");
    }

    #[test]
    fn generate_ordering_by_stability() {
        let generator = CatalogGenerator::new();

        // Create entries with same cost but different sequences
        let mut entry1 = make_text_entry("Old stable entry", 1);
        entry1.integration_cost.catalog_shift = 0.5;
        let entry1_id = entry1.id;

        let mut entry2 = make_text_entry("Recent entry", 10);
        entry2.integration_cost.catalog_shift = 0.5;
        let entry2_id = entry2.id;

        let cluster1 = make_cluster(0, &["old"], vec![entry1_id]);
        let cluster2 = make_cluster(1, &["recent"], vec![entry2_id]);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.timestamp.sequence = 20;
        snapshot.clusters.push(cluster1);
        snapshot.clusters.push(cluster2);

        let catalog = generator.generate(&snapshot, &[entry1, entry2], None);

        // Same cost, so older (more stable) cluster should come first
        assert_eq!(catalog.clusters.len(), 2);
        assert_eq!(catalog.clusters[0].topic, "old");
        assert!(catalog.clusters[0].stability > catalog.clusters[1].stability);
    }

    #[test]
    fn generate_truncate_to_budget() {
        let generator = CatalogGenerator::new();

        // Create many clusters
        let mut entries = Vec::new();
        let mut snapshot = CoherenceSnapshot::new();

        for i in 0..100 {
            let entry = make_text_entry(&format!("Entry {}", i), i as u64);
            let entry_id = entry.id;
            entries.push(entry);

            let cluster = make_cluster(i, &[&format!("topic{}", i)], vec![entry_id]);
            snapshot.clusters.push(cluster);
        }

        // Use small budget
        let catalog = generator.generate(&snapshot, &entries, Some(300)); // ~4 clusters

        assert!(catalog.clusters.len() <= 4);
        assert!(catalog.clusters.len() > 0);
    }

    #[test]
    fn generate_non_text_entries() {
        let generator = CatalogGenerator::new();

        let entry = EntryBuilder::default()
            .content(vec![0xFF, 0xD8, 0xFF, 0xE0]) // JPEG bytes
            .content_type("image/jpeg")
            .author(AuthorId::zero())
            .causal_position(CausalPosition::first())
            .integration_cost(IntegrationCost::zero())
            .build();
        let entry_id = entry.id;

        let cluster = make_cluster(0, &["image"], vec![entry_id]);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster);

        let catalog = generator.generate(&snapshot, &[entry], None);

        assert_eq!(catalog.clusters.len(), 1);
        // Should have fallback summary for non-text
        assert!(catalog.clusters[0].summary.contains("entries"));
    }

    #[test]
    fn cluster_summary_serialization() {
        let summary = ClusterSummary {
            topic: "test topic".to_string(),
            summary: "Test summary text.".to_string(),
            entry_count: 5,
            cumulative_cost: 1.5,
            stability: 10,
            representative_entry_ids: vec![EntryId::new()],
        };

        let json = serde_json::to_string(&summary).unwrap();
        let parsed: ClusterSummary = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.topic, summary.topic);
        assert_eq!(parsed.entry_count, summary.entry_count);
        assert_eq!(parsed.cumulative_cost, summary.cumulative_cost);
        assert_eq!(parsed.stability, summary.stability);
    }

    #[test]
    fn catalog_serialization() {
        let catalog = Catalog {
            clusters: vec![],
            notebook_entropy: 5.5,
            total_entries: 100,
            generated_at: CausalPosition::first(),
        };

        let json = serde_json::to_string(&catalog).unwrap();
        let parsed: Catalog = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.notebook_entropy, catalog.notebook_entropy);
        assert_eq!(parsed.total_entries, catalog.total_entries);
    }

    #[test]
    fn default_implementation() {
        let generator = CatalogGenerator::default();
        assert_eq!(generator.max_tokens(), DEFAULT_MAX_TOKENS);
    }

    #[test]
    fn representative_entry_ids_limited() {
        let generator = CatalogGenerator::new();

        // Create cluster with many entries
        let mut entries = Vec::new();
        let mut entry_ids = Vec::new();

        for i in 0..10 {
            let entry = make_text_entry(&format!("Entry {}", i), i as u64);
            entry_ids.push(entry.id);
            entries.push(entry);
        }

        let cluster = make_cluster(0, &["test"], entry_ids);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster);

        let catalog = generator.generate(&snapshot, &entries, None);

        // Should only include MAX_REPRESENTATIVE_ENTRIES
        assert!(catalog.clusters[0].representative_entry_ids.len() <= MAX_REPRESENTATIVE_ENTRIES);
    }

    #[test]
    fn cumulative_cost_sums_entries() {
        let generator = CatalogGenerator::new();

        let mut entry1 = make_text_entry("Entry 1", 1);
        entry1.integration_cost.catalog_shift = 0.3;

        let mut entry2 = make_text_entry("Entry 2", 2);
        entry2.integration_cost.catalog_shift = 0.7;

        let entry_ids = vec![entry1.id, entry2.id];
        let cluster = make_cluster(0, &["test"], entry_ids);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster);

        let catalog = generator.generate(&snapshot, &[entry1, entry2], None);

        // Cumulative cost should be 0.3 + 0.7 = 1.0
        assert!((catalog.clusters[0].cumulative_cost - 1.0).abs() < 0.001);
    }

    #[test]
    fn topic_limits_keywords() {
        let generator = CatalogGenerator::new();

        let entry = make_text_entry("Test entry", 1);
        let entry_id = entry.id;

        // Cluster with many keywords
        let cluster = make_cluster(0, &["one", "two", "three", "four", "five"], vec![entry_id]);

        let mut snapshot = CoherenceSnapshot::new();
        snapshot.clusters.push(cluster);

        let catalog = generator.generate(&snapshot, &[entry], None);

        // Topic should only include MAX_TOPIC_KEYWORDS
        let topic_parts: Vec<_> = catalog.clusters[0].topic.split(", ").collect();
        assert!(topic_parts.len() <= MAX_TOPIC_KEYWORDS);
    }
}
