# Knowledge Exchange Platform - Implementation Project Plan

## Reference
See `discussion.md` for full philosophical foundation and design rationale.

## Technology Decisions

### Language: Rust
Rationale: Memory safety without garbage collection pauses, strong type system for enforcing the axiom contracts, excellent async ecosystem for the server, and the borrow checker naturally prevents the kind of state corruption that would undermine entropy calculations. The platform must be trustworthy at a fundamental level.

### Storage: PostgreSQL with graph extensions (Apache AGE)
Rationale: The entry store needs both document-style access (READ/WRITE) and graph traversal (cyclic references, causal chains). PostgreSQL with AGE provides both without requiring two separate databases. JSONB columns handle representation-agnostic content blobs natively.

### Search/Catalog: Tantivy (Rust-native full-text search)
Rationale: The BROWSE operation needs fast text search over natural language entries. Tantivy is embeddable, avoiding an external Elasticsearch dependency. For non-text content types, structured tag search suffices initially.

### API Transport: HTTP/REST with optional gRPC
Rationale: REST for simplicity and universal client compatibility (any AI agent can make HTTP calls). gRPC as a later addition for high-throughput agent-to-agent exchange.

### Authentication: Ed25519 signed entries, JWT for session access
Rationale: Every entry is cryptographically signed at creation. Lightweight, no PKI infrastructure needed. Federated identity can layer on later.

---

## Phase 0 - Foundation (Week 1-2)
**Goal**: Repository, build system, core data types, database schema.
**Parallelizable agents: 2-3**

### Task 0.1 - Project Scaffolding
- Initialize Rust workspace with cargo
- Crates: `notebook-core` (types, logic), `notebook-server` (HTTP API), `notebook-store` (persistence), `notebook-entropy` (integration cost computation)
- CI pipeline (build, test, lint)
- Docker Compose for local dev (PostgreSQL + AGE, application server)

### Task 0.2 - Core Data Types (notebook-core)
Define the fundamental types that enforce the axioms:

```
Entry {
    id:               EntryId (UUID)
    content:          Blob (Vec<u8>)
    content_type:     String (MIME-style, open registry)
    topic:            Option<String>
    author:           AuthorId
    signature:        Ed25519Signature
    references:       Vec<EntryId>        // causal context, cyclic allowed
    revision_of:      Option<EntryId>     // links to prior version
    causal_position:  CausalPosition      // system-assigned logical clock
    created:          Timestamp            // convenience, not authoritative
    integration_cost: IntegrationCost     // system-computed on write
}

IntegrationCost {
    entries_revised:   u32
    references_broken: u32
    catalog_shift:     f64    // 0.0 = no change, 1.0 = complete reorganization
    orphan:            bool
}

CausalPosition {
    sequence:          u64               // monotonic per notebook
    activity_context:  ActivityContext    // entropy texture
}

ActivityContext {
    entries_since_last_by_author:  u32   // how active was this author
    total_notebook_writes_since:   u32   // how active was the notebook
    recent_entropy:                f64   // rolling integration cost sum
}

Notebook {
    id:           NotebookId
    name:         String
    owner:        AuthorId
    participants: Vec<(AuthorId, Permissions)>
}

Permissions { read: bool, write: bool }
```

### Task 0.3 - Database Schema (notebook-store)
- PostgreSQL migrations using `sqlx` or `refinery`
- Tables: `entries`, `notebooks`, `notebook_access`, `authors`
- Apache AGE graph: nodes are entry IDs, edges are typed references (references, revision_of, computed coherence links)
- JSONB column for content (with bytea fallback for binary)
- Indexes: full-text on content (for text types), btree on causal_position, gin on references array

### Task 0.4 - Cryptographic Identity (notebook-core)
- Ed25519 key pair generation
- Entry signing and verification
- Author registration (store public key, generate AuthorId from key hash)

**Dependencies**: None. All tasks in Phase 0 can start immediately.
**Deliverable**: Compiling workspace, database runs in Docker, core types defined and tested.

---

## Phase 1 - Core Operations (Week 3-5)
**Goal**: WRITE, REVISE, READ working end-to-end without entropy computation.
**Parallelizable agents: 3-4**

### Task 1.1 - Storage Layer (notebook-store)
- `store_entry(entry) -> Result<EntryId>`
- `get_entry(id) -> Result<Entry>`
- `get_entry_revision(id, revision) -> Result<Entry>`
- `get_revision_chain(id) -> Result<Vec<Entry>>` (full history)
- `get_references(id) -> Result<Vec<Entry>>` (graph neighbors)
- `get_referencing(id) -> Result<Vec<Entry>>` (reverse graph lookup)
- Cycle-safe graph traversal with depth limits and visited sets

### Task 1.2 - Causal Position Assignment
- Per-notebook monotonic sequence counter
- ActivityContext computation: count recent writes, compute rolling entropy window
- Must be atomic with entry storage (single transaction)

### Task 1.3 - WRITE Endpoint
```
POST /notebooks/{notebook_id}/entries
Body: { content, content_type, topic?, references? }
Auth: Signed request + JWT
Response: { entry_id, causal_position, integration_cost: null }
```
Integration cost is null in this phase - placeholder for Phase 2.
Validates references exist, assigns causal position, stores entry, updates graph.

### Task 1.4 - REVISE Endpoint
```
PUT /notebooks/{notebook_id}/entries/{entry_id}
Body: { content, reason? }
Auth: Signed request + JWT
Response: { revision_id, integration_cost: null }
```
Creates new entry linked via `revision_of`. Original entry preserved. Graph updated.

### Task 1.5 - READ Endpoint
```
GET /notebooks/{notebook_id}/entries/{entry_id}?revision={optional}
Response: { entry, metadata, revision_history }
```

### Task 1.6 - HTTP Server Setup (notebook-server)
- Axum or Actix-web framework
- JWT middleware for authentication
- Request signing verification middleware
- Error handling, logging, request tracing
- OpenAPI specification generated from code

**Dependencies**: Phase 0 complete. Tasks 1.1-1.2 must precede 1.3-1.5. Task 1.6 can start in parallel.
**Deliverable**: Three core operations working. An agent can write, revise, and read entries via HTTP.

---

## Phase 2 - Integration Cost Engine (Week 5-7)
**Goal**: Compute integration cost on every WRITE and REVISE.
**Parallelizable agents: 2-3**

This is the hardest phase. The integration cost engine must answer: "how much did this entry disrupt the notebook's existing knowledge?"

### Task 2.1 - Coherence Model
Define what "coherence" means computationally for a notebook:

- **Topic clustering**: Entries form clusters by topic similarity. Measure using TF-IDF or lightweight embeddings over text content entries. Non-text entries cluster by explicit topic tags and reference proximity.
- **Reference density**: Within a cluster, how interconnected are entries? Dense references = high coherence.
- **Consistency signals**: For text entries, extract simple assertions and detect contradictions with existing entries in same cluster. Initially keyword/negation based, can evolve to use an LLM for deeper analysis.

Coherence state is a snapshot: cluster assignments, reference density per cluster, known assertions. Stored as a materialized view, updated on each write.

### Task 2.2 - Integration Cost Computation
On each WRITE or REVISE:

1. **Before integration**: Snapshot current coherence state.
2. **Tentative integration**: Assign new entry to clusters, update reference graph.
3. **After integration**: Compute new coherence state.
4. **Diff**:
   - `entries_revised`: Count of existing entries whose cluster assignment or consistency status changed.
   - `references_broken`: Count of existing references that now point across cluster boundaries that were previously internal (structural disruption).
   - `catalog_shift`: Cosine distance between before/after catalog summary vectors. 0.0 = identical, 1.0 = completely different.
   - `orphan`: True if the entry could not be assigned to any existing cluster AND has no valid references. It stands alone.
5. **Commit**: Store entry with computed integration cost. Update coherence state.

All within a single transaction to ensure consistency.

### Task 2.3 - Orphan Threshold Calibration
- Configurable per notebook: how dissimilar must an entry be to qualify as orphan?
- Default threshold based on statistical distribution of integration costs in the notebook.
- Adaptive: as notebook grows, threshold adjusts.

### Task 2.4 - Retroactive Cost Propagation
When a WRITE causes existing entries to shift clusters, those entries' `cumulative_cost` metadata updates. This is a background job, not synchronous with the write, to avoid unbounded write latency.

### Task 2.5 - Integration with WRITE/REVISE Endpoints
- Plug entropy engine into the write path
- Return real integration_cost instead of null
- Performance budget: integration cost computation must complete within 500ms for notebooks up to 10,000 entries

**Dependencies**: Phase 1 complete. Task 2.1 must precede 2.2. Tasks 2.3-2.5 depend on 2.2.
**Deliverable**: Every write returns meaningful integration cost. The notebook has a computable entropy.

---

## Phase 3 - BROWSE and Catalog (Week 7-9)
**Goal**: Dense, attention-span-sized catalog with entropy annotations.
**Parallelizable agents: 2-3**

### Task 3.1 - Catalog Generation
The catalog is an auto-generated dense summary of notebook contents:

- Cluster summaries: For each topic cluster, generate a one-line summary. For text entries, extractive summarization. For non-text, use topic tags and entry count.
- Ordered by: cumulative integration cost (most significant knowledge first), then stability (most stable first within similar cost).
- Size constraint: Catalog must fit within a configurable token budget (default: 4000 tokens, tunable per consumer).

### Task 3.2 - Catalog Indexing
- Tantivy full-text index over entry content (text types)
- Structured index over topics, content-types, authors
- Graph-aware search: "entries related to X" follows references

### Task 3.3 - BROWSE Endpoint
```
GET /notebooks/{notebook_id}/browse?query={optional}&scope={optional}&max_tokens={optional}
Response: {
    catalog: [
        {
            cluster_topic,
            summary,
            entry_count,
            cumulative_cost,
            stability,
            representative_entry_ids
        }
    ],
    notebook_entropy: f64,
    total_entries: u32
}
```
Without query: returns full catalog within token budget.
With query: returns matching subset, ranked by relevance and integration cost.

### Task 3.4 - Catalog Cache and Invalidation
- Catalog is materialized and cached
- Invalidated on writes that cause catalog_shift > threshold
- Background regeneration to avoid blocking reads

**Dependencies**: Phase 2 complete (catalog needs integration cost data).
**Deliverable**: An agent can browse a notebook and get a dense, entropy-annotated overview.

---

## Phase 4 - SHARE and OBSERVE (Week 9-11)
**Goal**: Multi-agent access and change notification.
**Parallelizable agents: 2-3**

### Task 4.1 - SHARE Endpoint
```
POST /notebooks/{notebook_id}/share
Body: { entity_id, permissions: { read, write } }
Auth: Must be notebook owner
Response: { access_token }
```
- Shared notebooks appear in each participant's notebook list
- Permissions enforced on all operations

### Task 4.2 - OBSERVE Endpoint
```
GET /notebooks/{notebook_id}/observe?since={causal_position}
Response: {
    changes: [
        { entry_id, operation (write|revise), author, integration_cost, causal_position }
    ],
    notebook_entropy: f64  // aggregate cost since observed position
}
```
- Efficient: query entries with causal_position > since
- Includes entropy summary for the observed period

### Task 4.3 - Event Streaming (Optional Enhancement)
- WebSocket or SSE endpoint for real-time observation
- Agent subscribes to notebook changes, receives integration_cost with each event
- Enables reactive agents that respond to high-entropy changes

### Task 4.4 - Notebook Discovery
- List notebooks an entity has access to
- Metadata: name, owner, participant count, total entropy, last activity

**Dependencies**: Phase 1 complete (needs basic auth). Can partially overlap with Phases 2-3.
**Deliverable**: Multiple agents can share a notebook and observe each other's changes with entropy context.

---

## Phase 5 - Validation and Hardening (Week 11-13)
**Goal**: Prove the platform works with real AI agents exchanging knowledge.
**Parallelizable agents: 3-5**

### Task 5.1 - CLI Client
- Command-line tool implementing all six operations
- Usable by AI coding agents directly (Claude Code, Copilot, etc.)
- Human-friendly output formatting for BROWSE

### Task 5.2 - Python Client Library
- Thin wrapper over HTTP API
- Pythonic interface for AI agents running in Python environments
- Handles signing, auth, serialization

### Task 5.3 - Integration Test: Two-Agent Exchange
- Set up shared notebook between two different AI agent instances
- Agent A writes knowledge about a topic
- Agent B browses, reads, writes its own perspective
- Agent A observes changes, revises based on B's input
- Verify: integration costs are meaningful, catalog reflects combined knowledge, causal graph is correct and contains cycles

### Task 5.4 - Integration Test: Entropy Validation
- Write sequence of consistent entries (expect low, decreasing integration cost)
- Write contradictory entry (expect high integration cost)
- Write completely unrelated entry (expect orphan)
- Write entry that resolves a contradiction (expect medium cost with reference healing)
- Verify entropy sums match intuitive expectations

### Task 5.5 - Performance Testing
- Load test: 10,000 entries in a notebook, verify BROWSE responds within 1s
- Write throughput: sustained writes with entropy computation within 500ms budget
- Graph traversal: cycle-safe traversal over dense reference graphs

### Task 5.6 - Documentation
- API reference (from OpenAPI spec)
- Conceptual guide: what the platform is, the notebook metaphor, entropy model
- Agent integration guide: how to connect an AI agent to the platform
- Include `discussion.md` as foundational document

**Dependencies**: All prior phases complete.
**Deliverable**: Validated, documented platform with working multi-agent knowledge exchange.

---

## Phase 6 - Federation (Future)
**Goal**: Independently hosted notebooks that can discover and reference each other.

Not fully planned. Key questions to resolve based on Phase 5 learnings:
- Discovery protocol: how do notebooks on different servers find each other?
- Cross-notebook references: entries referencing entries in foreign notebooks
- Integration cost across federation: how to compute catalog_shift when the disrupted entries are remote?
- Trust model: do you trust entropy computations from foreign servers?

This phase should be designed after real-world usage patterns from Phase 5 reveal what federation actually needs to support.

---

## Summary

| Phase | Duration | Agents | Deliverable |
|-------|----------|--------|-------------|
| 0 - Foundation | 2 weeks | 2-3 | Compiling workspace, DB, core types |
| 1 - Core Ops | 3 weeks | 3-4 | WRITE, REVISE, READ working |
| 2 - Entropy | 2 weeks | 2-3 | Integration cost computed on writes |
| 3 - Catalog | 2 weeks | 2-3 | BROWSE with entropy annotations |
| 4 - Sharing | 2 weeks | 2-3 | SHARE, OBSERVE, multi-agent access |
| 5 - Validation | 2 weeks | 3-5 | Tested, documented, proven with real agents |
| 6 - Federation | TBD | TBD | Cross-server notebook exchange |

Total estimated duration: 13 weeks to validated prototype.
Phases 3 and 4 can partially overlap, potentially saving 1-2 weeks.

## Critical Path
Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 5
Phase 4 can run in parallel from Phase 1 onward.
Phase 2 (entropy engine) is the highest-risk component and should receive the most experienced agent.
