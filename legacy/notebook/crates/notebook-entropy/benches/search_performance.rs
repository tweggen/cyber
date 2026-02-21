//! Search Performance Benchmark
//!
//! Measures the performance of Tantivy full-text search queries.
//! Target: < 100ms for 10,000 entry notebooks.
//!
//! This benchmark tests:
//! - SearchIndex.search() latency at various index sizes
//! - Query complexity impact (single term, multi-term, phrase)
//! - Index update (add/delete) performance
//!
//! Created by: agent-perf (Task 5-5)

use criterion::{BenchmarkId, Criterion, Throughput, black_box, criterion_group, criterion_main};
use notebook_core::types::{AuthorId, Entry, EntryBuilder, NotebookId};
use notebook_entropy::search::SearchIndex;
use rand::Rng;
use tempfile::TempDir;

/// Sample topics for generating realistic entries
const TOPICS: &[&str] = &[
    "machine learning algorithms",
    "neural network architectures",
    "data science pipelines",
    "distributed computing systems",
    "database optimization techniques",
    "security vulnerability analysis",
    "cryptographic protocols",
    "network infrastructure",
    "operating system kernels",
    "compiler optimization",
    "programming language design",
    "software architecture patterns",
    "testing methodologies",
    "deployment automation",
    "cloud computing platforms",
];

/// Content templates with searchable terms
const CONTENT_TEMPLATES: &[&str] = &[
    "The implementation of {} requires careful consideration of performance and scalability. \
     Key factors include memory usage, CPU utilization, and network latency.",
    "This document describes {} in the context of modern enterprise applications. \
     Integration with existing systems presents unique challenges and opportunities.",
    "Advanced techniques for {} optimization focus on reducing computational complexity. \
     Benchmarking results demonstrate significant improvements in throughput.",
    "Security analysis of {} reveals potential vulnerabilities in authentication flows. \
     Mitigation strategies include input validation and access control mechanisms.",
    "The evolution of {} over the past decade has transformed industry practices. \
     Emerging trends suggest continued innovation in this space.",
];

/// Generates a realistic text entry for indexing
fn generate_entry(rng: &mut impl Rng) -> (Entry, String) {
    let topic_idx = rng.gen_range(0..TOPICS.len());
    let topic = TOPICS[topic_idx];
    let template_idx = rng.gen_range(0..CONTENT_TEMPLATES.len());
    let content = CONTENT_TEMPLATES[template_idx].replace("{}", topic);

    let entry = EntryBuilder::default()
        .content(content.clone().into_bytes())
        .content_type("text/plain")
        .topic(topic)
        .author(AuthorId::zero())
        .build();

    (entry, topic.to_string())
}

/// Sets up a search index with N entries
fn setup_index(size: usize) -> (TempDir, SearchIndex, NotebookId, Vec<String>) {
    let temp_dir = TempDir::new().unwrap();
    let index = SearchIndex::new(temp_dir.path()).unwrap();
    let notebook_id = NotebookId::new();
    let mut rng = rand::thread_rng();
    let mut topics_used = Vec::new();

    for _ in 0..size {
        let (entry, topic) = generate_entry(&mut rng);
        topics_used.push(topic);
        index.index_entry(notebook_id, &entry).unwrap();
    }

    // Allow index to settle
    std::thread::sleep(std::time::Duration::from_millis(100));
    index.reload().unwrap();

    (temp_dir, index, notebook_id, topics_used)
}

/// Benchmarks search latency at various index sizes
fn search_latency_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("search_latency");

    // Test at various sizes: 100, 1000, 5000, 10000
    for size in [100, 1000, 5000, 10000] {
        let (_temp_dir, index, notebook_id, _topics) = setup_index(size);

        group.throughput(Throughput::Elements(1));
        group.bench_with_input(BenchmarkId::new("entries", size), &size, |b, _| {
            b.iter(|| {
                // Search for common term
                black_box(index.search("machine learning", notebook_id, 10).unwrap())
            })
        });
    }

    group.finish();
}

/// Benchmarks different query complexities
fn query_complexity_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("query_complexity");

    // Use 5000 entries as baseline
    let (_temp_dir, index, notebook_id, _topics) = setup_index(5000);

    // Single term query
    group.bench_function("single_term", |b| {
        b.iter(|| black_box(index.search("algorithm", notebook_id, 10).unwrap()))
    });

    // Two term query
    group.bench_function("two_terms", |b| {
        b.iter(|| black_box(index.search("machine learning", notebook_id, 10).unwrap()))
    });

    // Three term query
    group.bench_function("three_terms", |b| {
        b.iter(|| {
            black_box(
                index
                    .search("distributed computing systems", notebook_id, 10)
                    .unwrap(),
            )
        })
    });

    // Long query
    group.bench_function("long_query", |b| {
        b.iter(|| {
            black_box(
                index
                    .search(
                        "implementation performance scalability memory optimization",
                        notebook_id,
                        10,
                    )
                    .unwrap(),
            )
        })
    });

    // Rare term query
    group.bench_function("rare_term", |b| {
        b.iter(|| black_box(index.search("cryptographic", notebook_id, 10).unwrap()))
    });

    group.finish();
}

/// Benchmarks search with different result limits
fn result_limit_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("result_limit");

    // Use 5000 entries as baseline
    let (_temp_dir, index, notebook_id, _topics) = setup_index(5000);

    for limit in [1, 10, 50, 100] {
        group.bench_with_input(BenchmarkId::new("limit", limit), &limit, |b, &limit| {
            b.iter(|| {
                black_box(
                    index
                        .search("machine learning", notebook_id, limit)
                        .unwrap(),
                )
            })
        });
    }

    group.finish();
}

/// Benchmarks index update operations
fn index_update_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("index_update");
    let mut rng = rand::thread_rng();

    // Add entry to existing index
    {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();
        let notebook_id = NotebookId::new();

        // Pre-populate with 1000 entries
        for _ in 0..1000 {
            let (entry, _) = generate_entry(&mut rng);
            index.index_entry(notebook_id, &entry).unwrap();
        }

        group.bench_function("add_entry_to_1k", |b| {
            b.iter(|| {
                let (entry, _) = generate_entry(&mut rng);
                black_box(index.index_entry(notebook_id, &entry).unwrap())
            })
        });
    }

    // Delete entry from existing index
    {
        let temp_dir = TempDir::new().unwrap();
        let index = SearchIndex::new(temp_dir.path()).unwrap();
        let notebook_id = NotebookId::new();

        // Pre-populate with entries, keeping track of IDs
        let mut entry_ids = Vec::new();
        for _ in 0..1000 {
            let (entry, _) = generate_entry(&mut rng);
            entry_ids.push(entry.id);
            index.index_entry(notebook_id, &entry).unwrap();
        }

        let mut idx = 0;
        group.bench_function("delete_entry_from_1k", |b| {
            b.iter(|| {
                if idx < entry_ids.len() {
                    black_box(index.delete_entry(entry_ids[idx]).unwrap());
                    idx += 1;
                }
            })
        });
    }

    group.finish();
}

/// Benchmarks search immediately after index update
fn search_after_update_benchmark(c: &mut Criterion) {
    let mut group = c.benchmark_group("search_after_update");
    let mut rng = rand::thread_rng();

    let temp_dir = TempDir::new().unwrap();
    let index = SearchIndex::new(temp_dir.path()).unwrap();
    let notebook_id = NotebookId::new();

    // Pre-populate
    for _ in 0..1000 {
        let (entry, _) = generate_entry(&mut rng);
        index.index_entry(notebook_id, &entry).unwrap();
    }

    // Benchmark: add entry then immediately search
    group.bench_function("add_then_search", |b| {
        b.iter(|| {
            // Add new entry
            let (entry, _) = generate_entry(&mut rng);
            index.index_entry(notebook_id, &entry).unwrap();

            // Reload and search
            index.reload().unwrap();
            black_box(index.search("learning", notebook_id, 10).unwrap())
        })
    });

    group.finish();
}

criterion_group!(
    benches,
    search_latency_benchmark,
    query_complexity_benchmark,
    result_limit_benchmark,
    index_update_benchmark,
    search_after_update_benchmark,
);
criterion_main!(benches);
