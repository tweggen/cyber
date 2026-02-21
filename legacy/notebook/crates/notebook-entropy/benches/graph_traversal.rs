//! Graph Traversal Benchmark
//!
//! Measures the performance of cycle-safe graph traversal over entry references.
//! Target: Reasonable latency for dense, cyclic reference graphs.
//!
//! This benchmark tests:
//! - Reference graph construction with cycles
//! - Coherence snapshot operations on cyclic graphs
//! - Integration cost computation with high reference density
//!
//! Created by: agent-perf (Task 5-5)

use criterion::{BenchmarkId, Criterion, black_box, criterion_group, criterion_main};
use notebook_core::types::{AuthorId, CausalPosition, Entry, EntryBuilder, EntryId, NotebookId};
use notebook_entropy::coherence::CoherenceSnapshot;
use notebook_entropy::engine::IntegrationCostEngine;
use rand::Rng;

/// Generates an entry with specific references
fn generate_entry_with_refs(topic: &str, refs: Vec<EntryId>) -> Entry {
    let content = format!(
        "This entry discusses {} and relates to {} other entries. \
         The relationships form a complex knowledge graph.",
        topic,
        refs.len()
    );

    EntryBuilder::default()
        .content(content.into_bytes())
        .content_type("text/plain")
        .topic(topic)
        .author(AuthorId::zero())
        .references(refs)
        .causal_position(CausalPosition::first())
        .build()
}

/// Creates a linear chain of entries (A -> B -> C -> ...)
fn create_linear_chain(size: usize) -> (Vec<Entry>, NotebookId) {
    let notebook_id = NotebookId::new();
    let mut entries = Vec::with_capacity(size);

    // First entry has no references
    let first = generate_entry_with_refs("chain start", vec![]);
    entries.push(first);

    // Each subsequent entry references the previous
    for i in 1..size {
        let refs = vec![entries[i - 1].id];
        let entry = generate_entry_with_refs(&format!("chain node {}", i), refs);
        entries.push(entry);
    }

    (entries, notebook_id)
}

/// Creates a cyclic graph where entries form a ring
fn create_ring_graph(size: usize) -> (Vec<Entry>, NotebookId) {
    let notebook_id = NotebookId::new();
    let mut entries = Vec::with_capacity(size);

    // Pre-generate entry IDs for cyclic references
    let entry_ids: Vec<EntryId> = (0..size).map(|_| EntryId::new()).collect();

    // Create entries where each references the next (with wrap-around)
    for i in 0..size {
        let next_idx = (i + 1) % size;
        let refs = vec![entry_ids[next_idx]];

        let entry = EntryBuilder::default()
            .id(entry_ids[i])
            .content(format!("Ring node {} references node {}", i, next_idx).into_bytes())
            .content_type("text/plain")
            .topic("ring graph")
            .author(AuthorId::zero())
            .references(refs)
            .causal_position(CausalPosition::first())
            .build();

        entries.push(entry);
    }

    (entries, notebook_id)
}

/// Creates a dense graph where each entry references multiple others
fn create_dense_graph(size: usize, refs_per_entry: usize) -> (Vec<Entry>, NotebookId) {
    let notebook_id = NotebookId::new();
    let mut entries = Vec::with_capacity(size);
    let mut rng = rand::thread_rng();

    // Pre-generate entry IDs
    let entry_ids: Vec<EntryId> = (0..size).map(|_| EntryId::new()).collect();

    // Create entries with random references
    for i in 0..size {
        let mut refs = Vec::new();
        let max_refs = refs_per_entry.min(size - 1);

        while refs.len() < max_refs {
            let target_idx = rng.gen_range(0..size);
            if target_idx != i && !refs.contains(&entry_ids[target_idx]) {
                refs.push(entry_ids[target_idx]);
            }
        }

        let entry = EntryBuilder::default()
            .id(entry_ids[i])
            .content(
                format!(
                    "Dense graph node {} with {} references. \
                     This entry participates in complex knowledge relationships.",
                    i,
                    refs.len()
                )
                .into_bytes(),
            )
            .content_type("text/plain")
            .topic("dense graph")
            .author(AuthorId::zero())
            .references(refs)
            .causal_position(CausalPosition::first())
            .build();

        entries.push(entry);
    }

    (entries, notebook_id)
}

/// Creates a fully connected graph (every entry references every other)
fn create_fully_connected_graph(size: usize) -> (Vec<Entry>, NotebookId) {
    let notebook_id = NotebookId::new();
    let mut entries = Vec::with_capacity(size);

    // Pre-generate entry IDs
    let entry_ids: Vec<EntryId> = (0..size).map(|_| EntryId::new()).collect();

    // Each entry references all others
    for i in 0..size {
        let refs: Vec<EntryId> = entry_ids
            .iter()
            .enumerate()
            .filter(|(j, _)| *j != i)
            .map(|(_, id)| *id)
            .collect();

        let entry = EntryBuilder::default()
            .id(entry_ids[i])
            .content(
                format!(
                    "Fully connected node {} with {} references to all other nodes.",
                    i,
                    refs.len()
                )
                .into_bytes(),
            )
            .content_type("text/plain")
            .topic("fully connected")
            .author(AuthorId::zero())
            .references(refs)
            .causal_position(CausalPosition::first())
            .build();

        entries.push(entry);
    }

    (entries, notebook_id)
}

/// Benchmarks coherence snapshot construction with different graph structures
fn snapshot_construction_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("snapshot_construction");

    // Linear chain
    for size in [100, 500, 1000] {
        let (entries, _) = create_linear_chain(size);

        group.bench_with_input(BenchmarkId::new("linear", size), &entries, |b, entries| {
            b.iter(|| {
                let mut snapshot = CoherenceSnapshot::new();
                for entry in entries {
                    black_box(snapshot.add_entry(entry));
                }
                black_box(snapshot)
            })
        });
    }

    // Ring (cyclic)
    for size in [100, 500, 1000] {
        let (entries, _) = create_ring_graph(size);

        group.bench_with_input(BenchmarkId::new("ring", size), &entries, |b, entries| {
            b.iter(|| {
                let mut snapshot = CoherenceSnapshot::new();
                for entry in entries {
                    black_box(snapshot.add_entry(entry));
                }
                black_box(snapshot)
            })
        });
    }

    // Dense (5 refs per entry)
    for size in [100, 500, 1000] {
        let (entries, _) = create_dense_graph(size, 5);

        group.bench_with_input(BenchmarkId::new("dense_5", size), &entries, |b, entries| {
            b.iter(|| {
                let mut snapshot = CoherenceSnapshot::new();
                for entry in entries {
                    black_box(snapshot.add_entry(entry));
                }
                black_box(snapshot)
            })
        });
    }

    group.finish();
}

/// Benchmarks integration cost computation on cyclic graphs
fn integration_cost_cyclic_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("integration_cost_cyclic");

    // Pre-build engines with different graph structures
    for size in [100, 500, 1000] {
        // Ring graph
        {
            let (entries, notebook_id) = create_ring_graph(size);
            let mut engine = IntegrationCostEngine::new();

            // Initialize with existing entries
            for entry in &entries {
                engine.compute_cost(entry, notebook_id).unwrap();
            }

            // Benchmark adding new entry that references into the ring
            group.bench_with_input(BenchmarkId::new("ring", size), &size, |b, _| {
                b.iter(|| {
                    let new_entry = generate_entry_with_refs(
                        "new ring member",
                        vec![entries[0].id, entries[size / 2].id],
                    );
                    black_box(
                        engine
                            .compute_cost_preview(&new_entry, notebook_id)
                            .unwrap(),
                    )
                })
            });
        }
    }

    group.finish();
}

/// Benchmarks reference density impact on integration cost
fn reference_density_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("reference_density");

    let base_size = 500;

    // Test with varying reference counts per entry
    for refs_per_entry in [1, 3, 5, 10] {
        let (entries, notebook_id) = create_dense_graph(base_size, refs_per_entry);
        let mut engine = IntegrationCostEngine::new();

        // Initialize engine
        for entry in &entries {
            engine.compute_cost(entry, notebook_id).unwrap();
        }

        // Benchmark adding entry with same density
        group.bench_with_input(
            BenchmarkId::new("refs", refs_per_entry),
            &refs_per_entry,
            |b, &refs| {
                b.iter(|| {
                    let ref_targets: Vec<EntryId> =
                        entries.iter().take(refs).map(|e| e.id).collect();
                    let new_entry = generate_entry_with_refs("dense entry", ref_targets);
                    black_box(
                        engine
                            .compute_cost_preview(&new_entry, notebook_id)
                            .unwrap(),
                    )
                })
            },
        );
    }

    group.finish();
}

/// Benchmarks worst-case: fully connected small graph
fn fully_connected_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("fully_connected");

    // Note: fully connected graphs grow O(n^2) in references, so keep sizes small
    for size in [10, 20, 50] {
        let (entries, notebook_id) = create_fully_connected_graph(size);
        let mut engine = IntegrationCostEngine::new();

        // Initialize engine
        for entry in &entries {
            engine.compute_cost(entry, notebook_id).unwrap();
        }

        // Benchmark adding entry that references all existing
        group.bench_with_input(BenchmarkId::new("nodes", size), &size, |b, _| {
            b.iter(|| {
                let all_refs: Vec<EntryId> = entries.iter().map(|e| e.id).collect();
                let new_entry = generate_entry_with_refs("fully connected new", all_refs);
                black_box(
                    engine
                        .compute_cost_preview(&new_entry, notebook_id)
                        .unwrap(),
                )
            })
        });
    }

    group.finish();
}

/// Benchmarks cycle detection in reference resolution
fn cycle_safety_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("cycle_safety");

    // Create graph with multiple overlapping cycles
    let size = 200;
    let notebook_id = NotebookId::new();
    let entry_ids: Vec<EntryId> = (0..size).map(|_| EntryId::new()).collect();
    let mut entries = Vec::with_capacity(size);

    // Create entries with overlapping cycle patterns
    for i in 0..size {
        // Each entry references:
        // - Next entry (forming a chain)
        // - Entry at offset size/4 (forming another cycle)
        // - Entry at offset size/2 (forming yet another cycle)
        let mut refs = vec![];

        if i + 1 < size {
            refs.push(entry_ids[i + 1]);
        }
        refs.push(entry_ids[(i + size / 4) % size]);
        refs.push(entry_ids[(i + size / 2) % size]);

        let entry = EntryBuilder::default()
            .id(entry_ids[i])
            .content(
                format!(
                    "Overlapping cycles node {} with {} cycle-forming references.",
                    i,
                    refs.len()
                )
                .into_bytes(),
            )
            .content_type("text/plain")
            .topic("overlapping cycles")
            .author(AuthorId::zero())
            .references(refs)
            .causal_position(CausalPosition::first())
            .build();

        entries.push(entry);
    }

    let mut engine = IntegrationCostEngine::new();
    for entry in &entries {
        engine.compute_cost(entry, notebook_id).unwrap();
    }

    // Benchmark: add entry that creates additional cycles
    group.bench_function("overlapping_cycles_200", |b| {
        b.iter(|| {
            let refs = vec![
                entry_ids[0],
                entry_ids[size / 4],
                entry_ids[size / 2],
                entry_ids[3 * size / 4],
            ];
            let new_entry = generate_entry_with_refs("cycle connector", refs);
            black_box(
                engine
                    .compute_cost_preview(&new_entry, notebook_id)
                    .unwrap(),
            )
        })
    });

    group.finish();
}

criterion_group!(
    benches,
    snapshot_construction_benchmark,
    integration_cost_cyclic_benchmark,
    reference_density_benchmark,
    fully_connected_benchmark,
    cycle_safety_benchmark,
);
criterion_main!(benches);
