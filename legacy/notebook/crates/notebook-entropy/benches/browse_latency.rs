//! Browse Latency Benchmark
//!
//! Measures the performance of catalog generation for the BROWSE endpoint.
//! Target: < 1 second for 10,000 entry notebooks.
//!
//! This benchmark tests:
//! - CatalogGenerator.generate() performance
//! - Scaling behavior from 100 to 10,000 entries
//! - Token budget impact on generation time
//!
//! Created by: agent-perf (Task 5-5)

use criterion::{BenchmarkId, Criterion, Throughput, black_box, criterion_group, criterion_main};
use notebook_core::types::{AuthorId, CausalPosition, Entry, EntryBuilder, IntegrationCost};
use notebook_entropy::catalog::CatalogGenerator;
use notebook_entropy::coherence::CoherenceSnapshot;
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
    "cloud computing",
    "microservices",
    "containers",
    "kubernetes",
    "monitoring",
];

/// Content templates for varied entry generation
const CONTENT_TEMPLATES: &[&str] = &[
    "This document covers the fundamentals of {} and its applications in modern systems.",
    "An overview of {} principles, including best practices and common pitfalls.",
    "Advanced techniques in {} for improved performance and reliability.",
    "The relationship between {} and other system components is explored here.",
    "Key metrics and monitoring strategies for {} deployments.",
    "Security considerations when implementing {} in production environments.",
    "Scaling {} horizontally across multiple nodes and data centers.",
    "Debugging and troubleshooting common {} issues in distributed systems.",
    "Integration patterns for {} with existing enterprise infrastructure.",
    "Future directions and emerging trends in {} technology.",
];

/// Generates a realistic text entry with integration cost
fn generate_entry(rng: &mut impl Rng, sequence: u64) -> Entry {
    // Select random topic and template
    let topic_idx = rng.gen_range(0..TOPICS.len());
    let topic = TOPICS[topic_idx];
    let template_idx = rng.gen_range(0..CONTENT_TEMPLATES.len());
    let content = CONTENT_TEMPLATES[template_idx].replace("{}", topic);

    // Generate varied integration cost
    let catalog_shift = rng.gen_range(0.0..1.0);

    EntryBuilder::default()
        .content(content.into_bytes())
        .content_type("text/plain")
        .topic(topic)
        .author(AuthorId::zero())
        .causal_position(CausalPosition {
            sequence,
            ..Default::default()
        })
        .integration_cost(IntegrationCost {
            entries_revised: rng.gen_range(0..5),
            references_broken: rng.gen_range(0..3),
            catalog_shift,
            orphan: catalog_shift > 0.9,
        })
        .build()
}

/// Sets up a coherence snapshot with N entries for benchmarking
fn setup_snapshot_and_entries(size: usize) -> (CoherenceSnapshot, Vec<Entry>) {
    let mut snapshot = CoherenceSnapshot::new();
    let mut entries = Vec::with_capacity(size);
    let mut rng = rand::thread_rng();

    for i in 0..size {
        let entry = generate_entry(&mut rng, i as u64 + 1);
        snapshot.add_entry(&entry);
        entries.push(entry);
    }

    // Update snapshot timestamp
    snapshot.timestamp.sequence = size as u64;

    (snapshot, entries)
}

/// Benchmarks catalog generation at various notebook sizes
fn browse_latency_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("browse_latency");
    let generator = CatalogGenerator::new();

    // Test at various sizes: 100, 1000, 5000, 10000
    for size in [100, 1000, 5000, 10000] {
        let (snapshot, entries) = setup_snapshot_and_entries(size);

        group.throughput(Throughput::Elements(size as u64));
        group.bench_with_input(BenchmarkId::new("entries", size), &size, |b, _| {
            b.iter(|| black_box(generator.generate(&snapshot, &entries, None)))
        });
    }

    group.finish();
}

/// Benchmarks catalog generation with different token budgets
fn token_budget_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("token_budget");

    // Use 5000 entries as baseline
    let (snapshot, entries) = setup_snapshot_and_entries(5000);

    // Test various token budgets
    for budget in [1000, 2000, 4000, 8000] {
        let generator = CatalogGenerator::with_max_tokens(budget);

        group.bench_with_input(BenchmarkId::new("tokens", budget), &budget, |b, _| {
            b.iter(|| black_box(generator.generate(&snapshot, &entries, Some(budget))))
        });
    }

    group.finish();
}

/// Benchmarks catalog generation for sparse vs dense topic distributions
fn topic_distribution_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("topic_distribution");
    let generator = CatalogGenerator::new();

    // Dense: All entries about same topic
    {
        let mut snapshot = CoherenceSnapshot::new();
        let mut entries = Vec::with_capacity(1000);

        for i in 0..1000 {
            let content = format!(
                "Machine learning model {} uses neural networks. \
                 Deep learning is a subset of machine learning.",
                i
            );
            let entry = EntryBuilder::default()
                .content(content.into_bytes())
                .content_type("text/plain")
                .topic("machine learning")
                .author(AuthorId::zero())
                .causal_position(CausalPosition {
                    sequence: i as u64 + 1,
                    ..Default::default()
                })
                .integration_cost(IntegrationCost {
                    catalog_shift: 0.1,
                    ..Default::default()
                })
                .build();
            snapshot.add_entry(&entry);
            entries.push(entry);
        }

        group.bench_function("dense_1k", |b| {
            b.iter(|| black_box(generator.generate(&snapshot, &entries, None)))
        });
    }

    // Sparse: Each entry has unique topic
    {
        let mut snapshot = CoherenceSnapshot::new();
        let mut entries = Vec::with_capacity(1000);

        for i in 0..1000 {
            let topic = format!("unique_topic_{}", i);
            let content = format!("Content for {} with unique vocabulary words.", topic);
            let entry = EntryBuilder::default()
                .content(content.into_bytes())
                .content_type("text/plain")
                .topic(&topic)
                .author(AuthorId::zero())
                .causal_position(CausalPosition {
                    sequence: i as u64 + 1,
                    ..Default::default()
                })
                .integration_cost(IntegrationCost {
                    catalog_shift: 0.8,
                    orphan: true,
                    ..Default::default()
                })
                .build();
            snapshot.add_entry(&entry);
            entries.push(entry);
        }

        group.bench_function("sparse_1k", |b| {
            b.iter(|| black_box(generator.generate(&snapshot, &entries, None)))
        });
    }

    group.finish();
}

/// Benchmarks incremental catalog update simulation
fn incremental_update_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("incremental_update");
    let generator = CatalogGenerator::new();

    // Start with 9900 entries, add 100 more
    let (mut snapshot, mut entries) = setup_snapshot_and_entries(9900);
    let mut rng = rand::thread_rng();

    // Benchmark generating catalog after adding entries
    group.bench_function("add_100_to_9900", |b| {
        b.iter(|| {
            // Add 100 entries
            for i in 0..100 {
                let entry = generate_entry(&mut rng, 9901 + i as u64);
                snapshot.add_entry(&entry);
                entries.push(entry);
            }
            black_box(generator.generate(&snapshot, &entries, None))
        })
    });

    group.finish();
}

criterion_group!(
    benches,
    browse_latency_benchmark,
    token_budget_benchmark,
    topic_distribution_benchmark,
    incremental_update_benchmark,
);
criterion_main!(benches);
