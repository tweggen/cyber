//! Write Throughput Benchmark
//!
//! Measures the performance of WRITE operations with entropy computation.
//! Target: p99 < 500ms for notebooks up to 10,000 entries.
//!
//! This benchmark tests the IntegrationCostEngine.compute_cost() method,
//! which is the core operation invoked on every WRITE.
//!
//! Created by: agent-perf (Task 5-5)

use criterion::{BenchmarkId, Criterion, Throughput, black_box, criterion_group, criterion_main};
use notebook_core::types::{AuthorId, CausalPosition, Entry, EntryBuilder, EntryId, NotebookId};
use notebook_entropy::engine::IntegrationCostEngine;
use rand::Rng;

/// Sample topics for generating realistic entries
const TOPICS: &[&str] = &[
    "machine learning",
    "neural networks",
    "data science",
    "algorithms",
    "distributed systems",
    "databases",
    "security",
    "cryptography",
    "networking",
    "operating systems",
    "compilers",
    "programming languages",
    "software architecture",
    "testing",
    "devops",
];

/// Sample content fragments for generating realistic entries
const CONTENT_FRAGMENTS: &[&str] = &[
    "This document describes the implementation of",
    "The algorithm works by iteratively computing",
    "We propose a novel approach to solving",
    "Key observations from the analysis include",
    "The performance characteristics show that",
    "Integration with existing systems requires",
    "The theoretical foundation is based on",
    "Experimental results demonstrate that",
    "The design pattern facilitates",
    "Error handling strategies include",
    "Scalability considerations suggest that",
    "The security model ensures that",
    "Memory management is optimized by",
    "Concurrent access is handled through",
    "The API contract specifies that",
];

/// Generates a realistic text entry with varied content
fn generate_entry(rng: &mut impl Rng, existing_ids: &[EntryId]) -> Entry {
    // Select random topic
    let topic_idx = rng.gen_range(0..TOPICS.len());
    let topic = TOPICS[topic_idx];

    // Generate content by combining fragments
    let num_fragments = rng.gen_range(3..8);
    let mut content = String::new();
    for _ in 0..num_fragments {
        let frag_idx = rng.gen_range(0..CONTENT_FRAGMENTS.len());
        content.push_str(CONTENT_FRAGMENTS[frag_idx]);
        content.push(' ');
        content.push_str(topic);
        content.push_str(". ");
    }

    // Optionally add references (30% chance per existing entry, max 5)
    let mut references = Vec::new();
    if !existing_ids.is_empty() {
        let max_refs = 5.min(existing_ids.len());
        let num_refs = rng.gen_range(0..=max_refs);
        for _ in 0..num_refs {
            let ref_idx = rng.gen_range(0..existing_ids.len());
            if !references.contains(&existing_ids[ref_idx]) {
                references.push(existing_ids[ref_idx]);
            }
        }
    }

    EntryBuilder::default()
        .content(content.into_bytes())
        .content_type("text/plain")
        .topic(topic)
        .author(AuthorId::zero())
        .references(references)
        .causal_position(CausalPosition::first())
        .build()
}

/// Pre-populates an engine with N entries for benchmarking
fn setup_engine(size: usize) -> (IntegrationCostEngine, NotebookId, Vec<EntryId>) {
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();
    let mut rng = rand::thread_rng();
    let mut entry_ids = Vec::with_capacity(size);

    for _ in 0..size {
        let entry = generate_entry(&mut rng, &entry_ids);
        entry_ids.push(entry.id);
        engine.compute_cost(&entry, notebook_id).unwrap();
    }

    (engine, notebook_id, entry_ids)
}

/// Benchmarks write throughput at various notebook sizes
fn write_throughput_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("write_throughput");

    // Test at various sizes: 100, 1000, 5000, 10000
    for size in [100, 1000, 5000, 10000] {
        // Setup engine with pre-populated entries
        let (mut engine, notebook_id, entry_ids) = setup_engine(size);
        let mut rng = rand::thread_rng();

        group.throughput(Throughput::Elements(1));
        group.bench_with_input(BenchmarkId::new("entries", size), &size, |b, _| {
            b.iter(|| {
                let entry = generate_entry(&mut rng, &entry_ids);
                black_box(engine.compute_cost(&entry, notebook_id).unwrap())
            })
        });
    }

    group.finish();
}

/// Benchmarks sustained write throughput (multiple sequential writes)
fn sustained_write_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("sustained_writes");

    // Start with 5000 entries, measure 100 consecutive writes
    let (mut engine, notebook_id, mut entry_ids) = setup_engine(5000);
    let mut rng = rand::thread_rng();

    group.throughput(Throughput::Elements(100));
    group.bench_function("100_writes_on_5k", |b| {
        b.iter(|| {
            for _ in 0..100 {
                let entry = generate_entry(&mut rng, &entry_ids);
                entry_ids.push(entry.id);
                black_box(engine.compute_cost(&entry, notebook_id).unwrap());
            }
        })
    });

    group.finish();
}

/// Benchmarks worst-case scenario: dissimilar entry causing cluster creation
fn orphan_write_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("orphan_writes");

    // Create engine with coherent entries (single topic)
    let mut engine = IntegrationCostEngine::new();
    let notebook_id = NotebookId::new();

    // Populate with machine learning entries
    for i in 0..1000 {
        let content = format!(
            "Machine learning model {} uses neural networks for training on datasets. \
             Deep learning algorithms optimize parameters through gradient descent.",
            i
        );
        let entry = EntryBuilder::default()
            .content(content.into_bytes())
            .content_type("text/plain")
            .topic("machine learning")
            .author(AuthorId::zero())
            .build();
        engine.compute_cost(&entry, notebook_id).unwrap();
    }

    // Benchmark writing a completely unrelated entry (orphan)
    group.bench_function("orphan_on_1k", |b| {
        b.iter(|| {
            let content = "Cooking recipes for Italian pasta dishes. \
                           Ingredients include tomatoes, basil, and olive oil.";
            let entry = EntryBuilder::default()
                .content(content.as_bytes().to_vec())
                .content_type("text/plain")
                .topic("cooking")
                .author(AuthorId::zero())
                .build();
            black_box(engine.compute_cost_preview(&entry, notebook_id).unwrap())
        })
    });

    group.finish();
}

criterion_group!(
    benches,
    write_throughput_benchmark,
    sustained_write_benchmark,
    orphan_write_benchmark,
);
criterion_main!(benches);
