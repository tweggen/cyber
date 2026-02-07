# Integration Cost (Entropy) Computation

## Core Insight

**Integration cost — the resistance to change when absorbing new knowledge — IS entropy.** This provides a time arrow without clock synchronization. High-cost periods represent rapid disruption; low-cost periods represent consolidation. The sum of integration costs over any period is the notebook's entropy.

## Integration Cost Structure

Every entry receives a computed `IntegrationCost` with four components:

| Component | Type | Meaning |
|---|---|---|
| `entries_revised` | `u32` | Number of existing entries whose cluster assignment changed |
| `references_broken` | `u32` | Number of the new entry's references that cross cluster boundaries |
| `catalog_shift` | `f64` (0.0–1.0) | How much the BROWSE summary reorganized (1 − cosine similarity of before/after catalog vectors) |
| `orphan` | `bool` | Entry landed in a new singleton cluster with no references — could not be integrated |

## Computation Pipeline

```
Entry WRITE
  │
  ▼
┌─────────────────────────────────────┐
│  1. Load CoherenceSnapshot          │
│     (in-memory per notebook)        │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  2. Capture BEFORE state            │
│     • entry → cluster mapping       │
│     • catalog vector (merged        │
│       TF-IDF of all cluster         │
│       keywords)                     │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  3. Add entry to snapshot           │
│     • Tokenize → TF-IDF vector     │
│     • Find best cluster by cosine   │
│       similarity (≥ threshold)      │
│     • Or create singleton cluster   │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  4. Capture AFTER state             │
│     • Rebuild entry → cluster map   │
│     • Recompute catalog vector      │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  5. Diff BEFORE / AFTER             │
│     • entries_revised = count of    │
│       cluster membership changes    │
│     • references_broken = entry     │
│       refs crossing boundaries      │
│     • catalog_shift = 1.0 −         │
│       cosine_sim(before, after)     │
│     • orphan = new cluster AND      │
│       no references                 │
└──────────────┬──────────────────────┘
               │
               ▼
  Return IntegrationCost
               │
               ▼
  (async) Enqueue retroactive propagation jobs
```

## Subsystem Details

### TF-IDF Similarity (`tfidf.rs`)

Lightweight text similarity without external NLP dependencies.

- **Tokenization**: Unicode word segmentation, lowercased, stop words filtered, minimum 2 characters.
- **Term Frequency**: `TF = count(term) / total_terms_in_document`
- **Inverse Document Frequency**: `IDF = ln(N / document_frequency)`
- **Weight**: `TF-IDF = TF × IDF`, stored as sparse vectors (`HashMap<String, f64>`)
- **Similarity**: Cosine similarity — `(V1 · V2) / (‖V1‖ × ‖V2‖)`, returns 0.0 for empty vectors.

### Agglomerative Clustering (`clustering.rs`)

Bottom-up hierarchical clustering based on semantic similarity.

1. Start with each entry as a singleton cluster.
2. Find the pair of clusters with the highest cosine similarity.
3. If similarity ≥ threshold (default **0.3**), merge them.
4. Recompute the merged cluster's keywords (top 5 by weight) and reference density.
5. Repeat until no pair exceeds the threshold or `max_clusters` is reached.

**Reference density** within a cluster:
```
density = internal_edges / (n × (n−1) / 2)
```
Singletons have density 1.0 by convention. Cyclic references are allowed.

### Coherence Snapshots (`coherence.rs`)

Persistent, serializable state of the notebook's knowledge organization.

```
CoherenceSnapshot
  ├── clusters: Vec<Cluster>
  ├── corpus_stats: CorpusStats          (for IDF computation)
  ├── cluster_vectors: HashMap<ClusterId, TfIdfVector>
  ├── entry_vectors: HashMap<EntryId, TfIdfVector>
  ├── reference_graph: ReferenceGraph
  ├── timestamp: CausalPosition
  ├── config: ClusteringConfig
  └── next_cluster_id: u64
```

Supports incremental updates (add single entry) and full rebuilds (from all entries on notebook load).

### Orphan Threshold Calibration (`calibration.rs`)

Adaptive threshold using **Welford's online algorithm** for numerical stability.

```
threshold = mean(catalog_shift) + 2 × stddev(catalog_shift)
```

This captures ~95% of observations under a normal distribution. Falls back to **0.7** if fewer than 10 observations exist. Can be overridden with a manual fixed threshold per notebook.

### Retroactive Cost Propagation (`propagation.rs`)

When adding an entry shifts clusters, existing entries may need their cumulative costs updated. This runs asynchronously via a job queue:

- **PropagationJob**: Identifies the notebook, affected entry IDs, and cost delta.
- **PropagationQueue**: Thread-safe `VecDeque` behind `Arc<Mutex<...>>`.
- **PropagationWorker**: Async processor; jobs are idempotent (deduplicated by UUID).

The write path completes without waiting for propagation.

### Catalog Generation (`catalog.rs`)

Converts coherence snapshots into dense summaries for the BROWSE operation.

1. Generate a `ClusterSummary` per cluster (topic keywords, extractive summary, cumulative cost, stability score, representative entry IDs).
2. Sort by cumulative cost descending, then stability descending.
3. Truncate to fit the **token budget** (default 4000 tokens ≈ 75 tokens/summary ≈ 53 summaries).
4. Return a `Catalog` with `notebook_entropy = Σ(cumulative_costs)`.

### Catalog Caching (`cache.rs`)

Stale-while-revalidate cache with configurable thresholds:

| Parameter | Default | Meaning |
|---|---|---|
| `shift_threshold` | 0.1 | Catalog shift below this skips cache invalidation |
| `max_age_secs` | 300 | Freshness window |
| `stale_grace_secs` | 60 | Serve stale data while regenerating |

## Why This Works as Entropy

- **Irreversible**: Entries don't un-integrate. Only new revisions change cluster structure.
- **Quantifiable**: Deterministic computation from observable state.
- **Meaningful**: Measures actual cognitive reorganization impact, not arbitrary metrics.
- **Time-agnostic**: Works for synchronous and asynchronous systems without wall clocks.
- **Causal**: High-cost entries permanently alter the notebook's trajectory — they are causally significant.

## Limitations

### Semantic Blindness (Bag-of-Words)

The TF-IDF similarity engine captures **statistical word distribution** but not **meaning**. Sentences with nearly identical vocabulary but opposite semantics produce nearly identical vectors:

| Sentence | Key tokens | Cosine similarity |
|---|---|---|
| "Tomorrow there will be rain" | `tomorrow`, `rain` | — |
| "Tomorrow there won't be rain" | `tomorrow`, `won't`, `rain` | ~0.95+ |

The engine would place these in the same cluster with low integration cost, despite being logically contradictory. This extends to:

- **Negation**: "is safe" vs "isn't safe"
- **Quantifier shifts**: "all users can access" vs "no users can access"
- **Argument reversal**: "A causes B" vs "B causes A"
- **Hedging and modality**: "this will happen" vs "this might happen"

In general, any meaning carried by grammatical structure rather than word choice is invisible to the current algorithm.

### Non-Text Content

For non-text entries (binary blobs, images, structured data), cluster assignment falls back to **topic keyword substring matching** against existing cluster keywords. This is a coarse heuristic — two structurally different entries with overlapping metadata keywords would appear similar.

### Clustering Granularity

Agglomerative clustering with a fixed similarity threshold (default 0.3) may not suit all notebooks equally. Highly specialized notebooks (narrow vocabulary) may under-cluster, while broad notebooks may over-cluster. The threshold is global, not adaptive per cluster.

### Possible Mitigations

| Approach | What it captures | Trade-off |
|---|---|---|
| **Semantic embeddings** (sentence transformers) | Meaning, contradiction, paraphrase | Requires ML runtime or external API; breaks representation-agnosticism |
| **Natural language inference (NLI)** | Explicit entailment / contradiction classification | Heavy model; only applicable to text pairs |
| **Negation-aware tokenization** | Negation scope (e.g., "not_rain" as distinct token) | Language-specific rules; partial coverage |
| **Pluggable similarity backend** | Allows swapping TF-IDF for embeddings per notebook | Architectural complexity; maintains agnosticism as default |

The current design is intentionally **representation-agnostic** — the platform never interprets content. Adding semantic understanding would improve accuracy of the entropy measure but would couple the engine to language-specific NLP. A pluggable similarity backend would preserve the design philosophy while allowing richer analysis where available.

## Source Files

All paths relative to `notebook/crates/notebook-entropy/src/`:

| File | Lines | Purpose |
|---|---|---|
| `tfidf.rs` | ~430 | TF-IDF tokenization, vectorization, cosine similarity |
| `clustering.rs` | ~550 | Agglomerative clustering, reference density |
| `coherence.rs` | ~660 | Coherence snapshots, incremental and full rebuild |
| `engine.rs` | ~630 | Core cost computation (before/after diff) |
| `calibration.rs` | ~620 | Adaptive orphan threshold (Welford's algorithm) |
| `propagation.rs` | ~150 | Async retroactive cost propagation jobs |
| `catalog.rs` | ~200 | Token-budgeted catalog generation |
| `cache.rs` | ~150 | Stale-while-revalidate catalog cache |

Integration cost type defined in `notebook/crates/notebook-core/src/types.rs`.
