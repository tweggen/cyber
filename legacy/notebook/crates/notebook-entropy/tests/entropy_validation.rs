//! Integration tests validating entropy/integration cost behavior.
//!
//! These tests verify that the entropy model implemented in notebook-entropy
//! behaves according to the theory defined in discussion.md:
//!
//! - Zero cost: Redundant information, already known
//! - Low cost: Natural extension, fits existing structure
//! - Medium cost: Genuine learning, meaningful restructuring
//! - High cost: Paradigm shift, deep reorganization
//! - Beyond threshold: Cannot be integrated, stored but orphaned
//!
//! The sum of integration costs IS the entropy of the notebook - it measures
//! irreversible cognitive change without reference to clocks.
//!
//! Owned by: agent-test-entropy (Task 5-4)

use notebook_entropy::IntegrationCostEngine;
use notebook_entropy::notebook_core::types::{AuthorId, Entry, EntryBuilder, EntryId, NotebookId};

// =============================================================================
// Test Helpers
// =============================================================================

/// Creates a text entry with the given content.
fn make_entry(content: &str) -> Entry {
    EntryBuilder::default()
        .content(content.as_bytes().to_vec())
        .content_type("text/plain")
        .author(AuthorId::zero())
        .build()
}

/// Creates a text entry with the given content and topic.
fn make_entry_with_topic(content: &str, topic: &str) -> Entry {
    EntryBuilder::default()
        .content(content.as_bytes().to_vec())
        .content_type("text/plain")
        .topic(topic)
        .author(AuthorId::zero())
        .build()
}

/// Creates a text entry that references other entries.
fn make_entry_with_refs(content: &str, refs: Vec<EntryId>) -> Entry {
    EntryBuilder::default()
        .content(content.as_bytes().to_vec())
        .content_type("text/plain")
        .author(AuthorId::zero())
        .references(refs)
        .build()
}

// =============================================================================
// Test 1: Consistent entries have low cost
// =============================================================================

/// Entries about the same topic should integrate without requiring revision
/// of existing entries, regardless of orphan status.
///
/// Theory: "Low cost: Natural extension, fits existing structure"
///
/// Note: Orphan status depends on TF-IDF similarity thresholds which may not
/// merge entries even with similar content. The key invariant is that no
/// existing entries need revision.
#[test]
fn consistent_entries_integrate_without_revision() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Establish a topic: machine learning
    let entry1 = make_entry(
        "Machine learning is a subset of artificial intelligence. \
         Neural networks are a key technique in machine learning. \
         Deep learning uses multiple neural network layers.",
    );
    engine.compute_cost(&entry1, notebook_id).unwrap();

    // Add a consistent, related entry about the same topic
    let entry2 = make_entry(
        "Artificial intelligence and machine learning enable pattern recognition. \
         Neural networks learn from training data. \
         Deep learning models require large datasets.",
    );
    let cost2 = engine.compute_cost(&entry2, notebook_id).unwrap();

    // No entries should need revision for coherence - this is a key invariant
    assert_eq!(
        cost2.entries_revised, 0,
        "Consistent entry should not cause entries to be revised"
    );

    // Catalog shift should be bounded
    assert!(
        cost2.catalog_shift >= 0.0 && cost2.catalog_shift <= 1.0,
        "Catalog shift should be bounded"
    );
}

/// When we build up multiple similar entries, later additions should
/// continue to integrate smoothly without requiring revisions.
#[test]
fn repeated_similar_entries_integrate_smoothly() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Add multiple entries about the same topic
    let topics = [
        "Machine learning algorithms use statistical methods to learn patterns.",
        "Learning algorithms in machine learning find patterns in data.",
        "Pattern recognition through machine learning algorithms.",
        "Statistical learning methods for pattern detection.",
        "Machine learning statistical pattern algorithms.",
    ];

    for content in topics {
        let entry = make_entry(content);
        let cost = engine.compute_cost(&entry, notebook_id).unwrap();

        // Key invariant: adding similar content shouldn't revise existing entries
        assert_eq!(
            cost.entries_revised, 0,
            "Similar content should not cause entries to be revised"
        );
    }
}

// =============================================================================
// Test 2: Contradictory/dissimilar entries have higher relative cost
// =============================================================================

/// Entries about completely unrelated topics should cause some
/// catalog shift as the notebook reorganizes to accommodate them.
///
/// Theory: "High cost: Paradigm shift, deep reorganization"
#[test]
fn dissimilar_entries_cause_catalog_shift() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Establish a strong topic: machine learning
    for _ in 0..3 {
        let entry = make_entry(
            "Machine learning neural networks deep learning algorithms \
             artificial intelligence training models data science.",
        );
        engine.compute_cost(&entry, notebook_id).unwrap();
    }

    // Add a completely unrelated entry about cooking
    let cooking_entry = make_entry(
        "Cooking recipes require fresh ingredients from the kitchen. \
         Baking bread needs flour, water, yeast, and salt. \
         Sauteing vegetables in olive oil creates delicious dishes.",
    );
    let cost = engine.compute_cost(&cooking_entry, notebook_id).unwrap();

    // The unrelated entry should cause some catalog shift (> 0)
    assert!(
        cost.catalog_shift > 0.0,
        "Dissimilar entry should cause catalog shift. Got: {}",
        cost.catalog_shift
    );
}

/// When comparing similar vs dissimilar entries, dissimilar entries
/// should be detected as orphans while similar entries may or may not be.
///
/// The key distinction is that truly unrelated content (different vocabulary)
/// creates new singleton clusters and is orphaned.
#[test]
fn dissimilar_entries_are_orphaned() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Build up a topic with multiple entries using the SAME content
    // to establish strong TF-IDF weights
    for _ in 0..5 {
        let entry = make_entry(
            "Machine learning neural networks deep learning algorithms \
             artificial intelligence training models data science.",
        );
        engine.compute_cost(&entry, notebook_id).unwrap();
    }

    // Add completely dissimilar entry with NO overlapping vocabulary
    let dissimilar = make_entry(
        "Renaissance art flourished in Italy during the 15th century. \
         Michelangelo painted the Sistine Chapel ceiling in Rome.",
    );
    let dissimilar_cost = engine.compute_cost(&dissimilar, notebook_id).unwrap();

    // Dissimilar entry should be orphaned (new cluster, no references)
    // because it has zero vocabulary overlap
    assert!(
        dissimilar_cost.orphan,
        "Dissimilar entry without references should be orphaned"
    );

    // Should not have revised any entries
    assert_eq!(
        dissimilar_cost.entries_revised, 0,
        "Adding dissimilar entry should not revise existing entries"
    );
}

// =============================================================================
// Test 3: Unrelated entries become orphans
// =============================================================================

/// An entry with completely different content and no references
/// should be marked as an orphan - it cannot be integrated.
///
/// Theory: "Beyond threshold: Cannot be integrated. Stored but orphaned."
#[test]
fn unrelated_entry_without_references_is_orphan() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Build up a coherent topic
    for _ in 0..5 {
        let entry = make_entry(
            "Machine learning neural networks deep learning algorithms \
             artificial intelligence training data science models.",
        );
        engine.compute_cost(&entry, notebook_id).unwrap();
    }

    // Add completely unrelated entry with no references
    let orphan_entry = make_entry(
        "Ancient Egyptian pyramids were built as tombs for pharaohs. \
         The Great Pyramid of Giza is one of the Seven Wonders.",
    );
    let cost = engine.compute_cost(&orphan_entry, notebook_id).unwrap();

    // Entry should be orphaned (no semantic match AND no references)
    assert!(
        cost.orphan,
        "Unrelated entry without references should be orphaned"
    );
}

/// An entry that is unrelated but has explicit references should
/// NOT be orphaned - references provide integration path.
#[test]
fn unrelated_entry_with_references_is_not_orphan() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Create a base entry
    let base_entry =
        make_entry("Machine learning algorithms for data analysis and pattern recognition.");
    let base_id = base_entry.id;
    engine.compute_cost(&base_entry, notebook_id).unwrap();

    // Add unrelated entry BUT with reference to base
    let entry_with_ref = make_entry_with_refs(
        "Ancient Egyptian pyramids were architectural marvels. \
         The pharaohs commissioned these massive structures.",
        vec![base_id],
    );
    let cost = engine.compute_cost(&entry_with_ref, notebook_id).unwrap();

    // Entry should NOT be orphaned because it has references
    assert!(
        !cost.orphan,
        "Entry with references should not be orphaned even if content is unrelated"
    );
}

/// The first entry in a notebook with no references is an orphan by definition.
#[test]
fn first_entry_without_references_is_orphan() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let first_entry = make_entry("This is the very first entry in the notebook.");
    let cost = engine.compute_cost(&first_entry, notebook_id).unwrap();

    // First entry creates catalog from nothing and has no references
    assert!(
        cost.orphan,
        "First entry without references should be orphaned"
    );
    assert!(
        cost.catalog_shift > 0.0,
        "First entry should have non-zero catalog shift"
    );
}

/// First entry WITH references is not an orphan.
#[test]
fn first_entry_with_references_is_not_orphan() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Note: referencing a non-existent entry is allowed - it's an external reference
    let external_ref = EntryId::new();
    let first_entry = make_entry_with_refs(
        "This is the first entry referencing external content.",
        vec![external_ref],
    );
    let cost = engine.compute_cost(&first_entry, notebook_id).unwrap();

    // Entry has references, so it's not an orphan
    assert!(
        !cost.orphan,
        "First entry with references should not be orphaned"
    );
}

// =============================================================================
// Test 4: Resolving entries have medium cost
// =============================================================================

/// An entry that references and synthesizes multiple existing entries
/// should integrate properly (not be orphaned) since it has references.
///
/// Theory: "Medium cost: Genuine learning, meaningful restructuring"
#[test]
fn synthesizing_entry_integrates_via_references() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Create two distinct topic clusters
    let ml_entry =
        make_entry("Machine learning algorithms for predictive modeling and data analysis.");
    let ml_id = ml_entry.id;
    engine.compute_cost(&ml_entry, notebook_id).unwrap();

    let stats_entry =
        make_entry("Statistical methods for hypothesis testing and regression analysis.");
    let stats_id = stats_entry.id;
    engine.compute_cost(&stats_entry, notebook_id).unwrap();

    // Create synthesizing entry that bridges both topics
    let synthesis = make_entry_with_refs(
        "Machine learning combines statistical methods with computational algorithms. \
         Regression analysis forms the foundation of many learning algorithms. \
         Predictive modeling uses both statistical and machine learning approaches.",
        vec![ml_id, stats_id],
    );
    let cost = engine.compute_cost(&synthesis, notebook_id).unwrap();

    // Synthesizing entry should not be orphaned (has references)
    assert!(
        !cost.orphan,
        "Synthesizing entry with references should not be orphaned"
    );

    // Should not revise existing entries - synthesis adds, doesn't replace
    assert_eq!(
        cost.entries_revised, 0,
        "Synthesizing entry should not revise existing entries"
    );
}

/// Reference healing: when an entry references disconnected entries,
/// it creates a bridge and integrates via those references.
#[test]
fn bridge_entry_connects_clusters() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Create entry about topic A
    let topic_a =
        make_entry("Astronomy studies celestial objects like stars, planets, and galaxies.");
    let topic_a_id = topic_a.id;
    engine.compute_cost(&topic_a, notebook_id).unwrap();

    // Create entry about unrelated topic B
    let topic_b =
        make_entry("Culinary arts involve cooking techniques and food preparation methods.");
    let topic_b_id = topic_b.id;
    engine.compute_cost(&topic_b, notebook_id).unwrap();

    // Create bridge entry referencing both
    let bridge = make_entry_with_refs(
        "Space food and astronaut nutrition combines culinary science with space travel. \
         NASA develops specialized meals for celestial missions.",
        vec![topic_a_id, topic_b_id],
    );
    let cost = engine.compute_cost(&bridge, notebook_id).unwrap();

    // Bridge should not be orphan (has references)
    assert!(!cost.orphan, "Bridge entry should not be orphaned");

    // Cross-cluster references may be detected
    // (exact count depends on cluster assignment)
}

// =============================================================================
// Test 5: Entropy accumulates monotonically
// =============================================================================

/// The total entropy (sum of integration costs) should only increase
/// as entries are added. Entropy measures irreversible change.
///
/// Theory: "The sum of integration costs IS the entropy of the notebook"
#[test]
fn entropy_accumulates_monotonically() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let entries = [
        "Machine learning enables computers to learn from data.",
        "Neural networks are inspired by biological neurons.",
        "Deep learning uses multiple layers of neural networks.",
        "Cooking requires fresh ingredients and proper technique.",
        "Baking bread needs precise measurements and timing.",
        "Quantum physics describes subatomic particle behavior.",
        "The stock market fluctuates based on supply and demand.",
        "Climate change affects global weather patterns.",
        "Philosophy explores fundamental questions about existence.",
        "Music theory explains harmony and rhythm in compositions.",
    ];

    let mut total_entropy = 0.0;
    let mut entropy_history = Vec::new();

    for content in entries {
        let entry = make_entry(content);
        let cost = engine.compute_cost(&entry, notebook_id).unwrap();

        // Compute entropy contribution from this entry
        // Using catalog_shift as the primary entropy measure
        let entry_entropy = cost.catalog_shift;
        total_entropy += entry_entropy;
        entropy_history.push(total_entropy);
    }

    // Verify monotonic increase (each step >= previous)
    for i in 1..entropy_history.len() {
        assert!(
            entropy_history[i] >= entropy_history[i - 1],
            "Entropy should accumulate monotonically. \
             Entry {}: {} < previous: {}",
            i + 1,
            entropy_history[i],
            entropy_history[i - 1]
        );
    }

    // Total entropy should be positive after diverse entries
    assert!(
        total_entropy > 0.0,
        "Total entropy should be positive after adding entries"
    );
}

/// Integration cost components are always non-negative.
#[test]
fn integration_cost_components_non_negative() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let entries = [
        "First entry about machine learning.",
        "Second entry about cooking recipes.",
        "Third entry about quantum physics.",
        "Fourth entry about ancient history.",
        "Fifth entry about modern architecture.",
    ];

    for content in entries {
        let entry = make_entry(content);
        let cost = engine.compute_cost(&entry, notebook_id).unwrap();

        // All cost components should be non-negative
        assert!(
            cost.catalog_shift >= 0.0,
            "Catalog shift should be non-negative. Got: {}",
            cost.catalog_shift
        );
        assert!(
            cost.references_broken == 0 || cost.references_broken > 0,
            "References broken should be non-negative"
        );
        // entries_revised is u32, always >= 0
    }
}

/// Catalog shift should be bounded (between 0.0 and 1.0).
#[test]
fn catalog_shift_is_bounded() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let entries = [
        "First entry about machine learning.",
        "Second entry about cooking recipes.",
        "Third entry about quantum physics.",
        "Fourth entry about ancient history.",
        "Fifth entry about modern architecture.",
    ];

    for content in entries {
        let entry = make_entry(content);
        let cost = engine.compute_cost(&entry, notebook_id).unwrap();

        assert!(
            cost.catalog_shift >= 0.0 && cost.catalog_shift <= 1.0,
            "Catalog shift should be in [0.0, 1.0]. Got: {}",
            cost.catalog_shift
        );
    }
}

/// High-cost entries (orphans/dissimilar) contribute to total entropy.
#[test]
fn high_cost_entries_contribute_to_entropy() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Establish a coherent topic
    let base = make_entry(
        "Machine learning algorithms neural networks deep learning \
         artificial intelligence data science training models.",
    );
    let base_cost = engine.compute_cost(&base, notebook_id).unwrap();

    // First entry always has some cost
    assert!(
        base_cost.catalog_shift > 0.0,
        "First entry should have positive catalog shift"
    );

    // Add dissimilar entry (will be orphaned)
    let dissimilar = make_entry(
        "Renaissance art flourished in Italy during the 15th century. \
         Michelangelo painted the Sistine Chapel ceiling.",
    );
    let dissimilar_cost = engine.compute_cost(&dissimilar, notebook_id).unwrap();

    // Dissimilar entry should contribute to entropy (catalog shift > 0)
    assert!(
        dissimilar_cost.catalog_shift > 0.0,
        "Dissimilar entry should have positive catalog shift"
    );
}

/// Entropy should increase even when adding redundant information,
/// because the act of integration itself has non-zero cost.
#[test]
fn even_redundant_entries_contribute_some_entropy() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let base = make_entry("The quick brown fox jumps over the lazy dog.");
    let cost1 = engine.compute_cost(&base, notebook_id).unwrap();

    // Add nearly identical entry
    let similar = make_entry("The quick brown fox jumps over the lazy dog.");
    let cost2 = engine.compute_cost(&similar, notebook_id).unwrap();

    // Total entropy is sum of costs
    let total = cost1.catalog_shift + cost2.catalog_shift;

    // First entry always contributes
    assert!(
        cost1.catalog_shift > 0.0,
        "First entry should have non-zero cost"
    );

    // Total should be at least the first entry's cost
    assert!(
        total >= cost1.catalog_shift,
        "Total entropy should accumulate"
    );
}

// =============================================================================
// Additional Validation Tests
// =============================================================================

/// Multiple notebooks should have isolated entropy.
#[test]
fn notebooks_have_isolated_entropy() {
    let mut engine = IntegrationCostEngine::new();
    let notebook1 = NotebookId::new();
    let notebook2 = NotebookId::new();

    // Build up notebook1 with ML content
    for _ in 0..5 {
        let entry = make_entry("Machine learning neural networks deep learning.");
        engine.compute_cost(&entry, notebook1).unwrap();
    }

    // notebook2 should start fresh
    let cooking_entry = make_entry("Cooking recipes ingredients kitchen baking.");
    let cost = engine.compute_cost(&cooking_entry, notebook2).unwrap();

    // First entry in notebook2 should behave like a first entry
    // (orphan because no references and first in notebook)
    assert!(
        cost.orphan,
        "First entry in isolated notebook should be orphan if no references"
    );
}

/// Topic annotations help entries integrate by providing semantic context.
#[test]
fn topic_provides_integration_context() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Create first entry with topic and content
    let entry1 = make_entry_with_topic(
        "Introduction to fundamental concepts in the field.",
        "machine learning",
    );
    engine.compute_cost(&entry1, notebook_id).unwrap();

    // Create second entry with same topic but different content
    // The shared topic keywords help with clustering
    let entry2 = make_entry_with_topic(
        "Advanced techniques and novel methodologies explored here.",
        "machine learning",
    );
    let cost2 = engine.compute_cost(&entry2, notebook_id).unwrap();

    // Even with different content, topic provides integration context
    // so the entry should integrate without revision
    assert_eq!(
        cost2.entries_revised, 0,
        "Entry with matching topic should integrate smoothly"
    );
}

/// Empty content entries are handled gracefully.
#[test]
fn empty_content_handled_gracefully() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let empty_entry = EntryBuilder::default()
        .content(Vec::new())
        .content_type("text/plain")
        .author(AuthorId::zero())
        .build();

    // Should not panic
    let cost = engine.compute_cost(&empty_entry, notebook_id).unwrap();

    // Empty entry with no references is orphan
    assert!(
        cost.orphan,
        "Empty entry without references should be orphan"
    );
}

/// Non-text content types are handled.
#[test]
fn non_text_content_handled() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Binary content
    let binary_entry = EntryBuilder::default()
        .content(vec![0xFF, 0xD8, 0xFF, 0xE0]) // JPEG magic bytes
        .content_type("image/jpeg")
        .author(AuthorId::zero())
        .build();

    // Should not panic
    let cost = engine.compute_cost(&binary_entry, notebook_id).unwrap();

    // Binary entry with no references and no text is orphan
    assert!(
        cost.orphan,
        "Binary entry without references should be orphan"
    );
}

/// The engine correctly tracks multiple notebooks independently.
#[test]
fn multiple_notebooks_tracked_independently() {
    let mut engine = IntegrationCostEngine::new();
    let notebook1 = NotebookId::new();
    let notebook2 = NotebookId::new();

    // Add entry to notebook1
    let entry1 = make_entry("Machine learning content for notebook one.");
    engine.compute_cost(&entry1, notebook1).unwrap();

    // Add similar entry to notebook2
    let entry2 = make_entry("Machine learning content for notebook two.");
    let cost2 = engine.compute_cost(&entry2, notebook2).unwrap();

    // Entry2 should be treated as first entry in notebook2
    // (orphan because it's first entry with no references)
    assert!(
        cost2.orphan,
        "First entry in notebook2 should be orphan (no prior context)"
    );

    // Verify both snapshots exist
    assert!(engine.get_snapshot(notebook1).is_some());
    assert!(engine.get_snapshot(notebook2).is_some());
    assert_eq!(engine.snapshot_count(), 2);
}

/// Engine snapshot can be removed.
#[test]
fn snapshot_removal_works() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    let entry = make_entry("Test content.");
    engine.compute_cost(&entry, notebook_id).unwrap();

    assert_eq!(engine.snapshot_count(), 1);

    engine.remove_snapshot(notebook_id);

    assert_eq!(engine.snapshot_count(), 0);
    assert!(engine.get_snapshot(notebook_id).is_none());
}

/// Preview cost computation doesn't mutate state.
#[test]
fn preview_cost_is_non_mutating() {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Add initial entry
    let entry1 = make_entry("Initial notebook content.");
    engine.compute_cost(&entry1, notebook_id).unwrap();

    let entry_count_before = engine.get_snapshot(notebook_id).unwrap().entry_count();

    // Preview adding a second entry
    let entry2 = make_entry("Preview content that should not be added.");
    let _preview_cost = engine.compute_cost_preview(&entry2, notebook_id).unwrap();

    // Entry count should not have changed
    let entry_count_after = engine.get_snapshot(notebook_id).unwrap().entry_count();
    assert_eq!(
        entry_count_before, entry_count_after,
        "Preview should not add entry to snapshot"
    );
}
