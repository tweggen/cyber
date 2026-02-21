# Claude Session Overview - Knowledge Exchange Platform

**Written**: 2026-02-07, based on full codebase exploration.
**Purpose**: Orient future Claude instances quickly on what this project is, what's built, what's missing, and what questions remain open.

---

## 1. What This Project Is

A **knowledge exchange platform** that serves as an externalized memory substrate for AI and biological entities. The core philosophical insight (from Timo, the project creator): **integration cost (resistance to change) IS entropy**, which provides a time arrow without clock synchronization.

The platform is NOT a database, wiki, or version control system. It is a **library that continuously redefines itself through use** - the ongoing process of an entity understanding what it knows. Memory is not a feature of an entity; memory is what makes something a perceived entity. Therefore the notebook IS the persistent, evolving part that constitutes identity.

Key design principles:
- **Representation-agnostic**: Content is opaque bytes + MIME type. The platform never interprets content.
- **Causal ordering, not timestamps**: Monotonic per-notebook sequence counter. No wall-clock dependency.
- **Integration cost as entropy**: New entries are measured by how hard they are to integrate into existing knowledge. Zero = redundant, low = natural extension, medium = genuine learning, high = paradigm shift, beyond threshold = orphan (analogous to PTSD).
- **Storage equals exchange**: Writing is sending a message to a future reader. Multi-agent access is inherent, not bolted on.
- **Cyclic references**: Knowledge has cycles. Understanding A deepens B which revises A. The reference graph is NOT a DAG.

See `discussion.md` for the full philosophical foundation and `project-plan.md` for the implementation roadmap.

---

## 2. Repository Structure

```
cyber/
  CLAUDE.md                          # Build instructions, architecture overview
  mcp/notebook_mcp.py                # MCP server for Claude Desktop (6 operations as tools)
  notebook/
    Cargo.toml                       # Rust workspace (edition 2024)
    Cargo.lock
    docker-compose.yml               # PostgreSQL + Apache AGE (+ planned server/frontend)
    Dockerfile                       # Rust server container
    README.md                        # Project readme
    bootstrap/
      bootstrap_notebook.py          # Minimal Python notebook server (738 lines)
      init_project.py                # Seeds bootstrap notebook with foundation entries
    cli/                             # Clap-based CLI with 9 subcommands
    crates/
      notebook-core/                 # Domain types, Ed25519 crypto, identity
      notebook-entropy/              # Integration cost engine (TF-IDF, clustering, catalog)
      notebook-store/                # PostgreSQL persistence via sqlx, Apache AGE graph
      notebook-server/               # Axum HTTP API (6 operations + management)
    migrations/
      init.sql                       # Apache AGE extension + graph creation
      002_schema.sql                 # Core tables: authors, notebooks, entries, access
    python/
      notebook_client/               # Python HTTP client (types, client, errors)
      pyproject.toml
    docs/
      discussion.md                  # Philosophical foundation (Timo + Claude, Feb 2026)
      project-plan.md                # 7-phase implementation roadmap
      concepts.md                    # Core concepts for developers
      api-reference.md               # HTTP REST API documentation
      agent-integration.md           # How to connect AI agents
      quickstart.md                  # Getting started guide
      integration-cost-summary.md    # Deep dive into entropy computation
      orchestrator-instructions.md   # Multi-agent orchestration guide
      plans/
        01-backend-auth-and-users.md # JWT auth, user management, quotas
        02-backend-management-api.md # Admin routes, usage logging
        03-blazor-frontend.md        # .NET 8 Blazor Server SPA
        04-deployment.md             # Coolify/Traefik to cyber.nassau-records.de
  .github/workflows/ci.yml          # Rust CI (build, test, clippy, fmt, docs)
```

---

## 3. What's Built and Working (Rust)

### notebook-core - 100% complete
- `EntryId`, `NotebookId`, `AuthorId` wrapper types with full serde support
- `Entry` with builder pattern, content (Vec<u8>), content_type, references, causal_position
- `IntegrationCost` struct: entries_revised, references_broken, catalog_shift, orphan
- `CausalPosition`: sequence (u64) + ActivityContext (rolling entropy context)
- Ed25519 crypto: KeyPair generation, entry signing/verification, BLAKE3-derived AuthorId
- 25+ unit tests, all passing

### notebook-entropy - ~95% complete
- **TF-IDF engine** (`tfidf.rs`): tokenization (Unicode word segmentation), term frequency, IDF via CorpusStats, cosine similarity between TfIdfVectors
- **Agglomerative clustering** (`clustering.rs`): bottom-up merging by TF-IDF similarity, configurable threshold (default 0.3), reference density tracking
- **Coherence model** (`coherence.rs`): CoherenceSnapshot maintains cluster state, updated per entry, captures before/after for cost computation
- **Integration cost engine** (`engine.rs`): compute_cost() does the full before/after diff, returns IntegrationCost. Supports preview (non-mutating), multi-notebook isolation, snapshot initialization from persisted entries
- **Orphan calibration** (`calibration.rs`): adaptive threshold via Welford's online algorithm (mean + 2*stddev)
- **Retroactive propagation** (`propagation.rs`): async job queue for updating affected entries after high-cost writes
- **Full-text search** (`search.rs`): Tantivy index, notebook-scoped queries, snippet generation
- **Catalog generation** (`catalog.rs`): token-budgeted (default 4000 tokens ~53 summaries), sorted by cumulative_cost then stability
- **Catalog caching** (`cache.rs`): stale-while-revalidate pattern, invalidation on high catalog_shift
- 30+ unit tests + comprehensive entropy_validation integration test (738 lines)
- **One known TODO**: ActivityContext.recent_entropy hardcoded to 0.0 in repository.rs (cross-crate integration, not missing algorithm)

### notebook-store - 100% complete
- `Store`: PgPool wrapper with connection pooling, migration runner
- Full CRUD for authors, entries, notebooks, access control
- `Repository`: domain-typed wrapper with cycle-safe graph traversal (max depth 100)
- Apache AGE graph support with SQL fallback when AGE unavailable
- Causal position service (monotonic per-notebook sequence)
- Specialized queries: notebook stats, orphan entries, broken references, topic filtering
- Schema migrations: init.sql (AGE setup), 002_schema.sql (all tables)

### notebook-server - 100% complete (6 operations + management)
- **Axum framework** with tower middleware stack
- **WRITE**: POST /notebooks/{id}/entries - creates entry, computes integration cost, updates entropy engine
- **REVISE**: PUT /notebooks/{id}/entries/{id} - revision chain preserved
- **READ**: GET /notebooks/{id}/entries/{id} - with revision history
- **BROWSE**: GET /notebooks/{id}/browse - token-budgeted catalog with entropy annotations
- **OBSERVE**: GET /notebooks/{id}/observe?since={seq} - changes since causal position
- **SHARE**: POST /notebooks/{id}/access - permission management
- **Notebook CRUD**: create, get, list
- **SSE**: Server-Sent Events for real-time updates (EventBroadcaster)
- **AppState**: Repository + IntegrationCostEngine + SearchIndex + CatalogCache + EventBroadcaster
- Health check, request ID middleware, CORS, graceful shutdown
- Integration test: two_agent_exchange.rs (882 lines, requires running server)

### CLI - 100% complete
- 9 subcommands: write, revise, read, browse, share, observe, list, create, delete
- reqwest HTTP client, --human vs JSON output, --url config
- Colored output, async with tokio

### Python client - complete
- HTTP client wrapping all 6 operations
- Dataclass types, custom error hierarchy
- In notebook/python/notebook_client/

### MCP integration - complete
- mcp/notebook_mcp.py: JSON-RPC MCP server for Claude Desktop
- Exposes all 6 operations as tools via stdin/stdout
- Configurable via NOTEBOOK_URL, NOTEBOOK_ID, NOTEBOOK_TOKEN, AUTHOR env vars
- JWT Bearer token authentication via NOTEBOOK_TOKEN

### Bootstrap server - complete
- bootstrap/bootstrap_notebook.py: minimal Python HTTP server (738 lines)
- Flat JSON file storage, hand-rolled TF-IDF, no auth
- Intended as throwaway scaffolding for orchestrating the real build

---

## 4. What's Planned but Not Yet Implemented

Four detailed implementation plans exist in `notebook/docs/plans/`:

### Plan 01 - Backend Auth & Users
- Migration 005_users: users table, user_keys, user_quotas, usage_log
- Argon2 password hashing, JWT token management
- Routes: /api/auth/login, /api/auth/logout, /api/auth/me, /api/auth/change-password
- Admin bootstrap: creates first admin user from env vars
- **Status**: Fully specified (~787 lines auth module, ~1071 lines auth routes), not coded

### Plan 02 - Backend Management API
- User CRUD routes (admin only)
- Quota management per user
- Usage logging (audit trail)
- Auth injection into all existing routes (currently no auth enforcement)
- **Status**: Fully specified, not coded

### Plan 03 - Blazor Frontend
- .NET 8 Blazor Server SPA
- Pages: Login, Dashboard, NotebookDetail, Users, UserDetail, UsageLog
- JWT-based AuthenticationStateProvider
- NotebookApiClient service (30+ methods)
- **Status**: Fully specified (16 files), not coded

### Plan 04 - Deployment
- Coolify + Traefik + Let's Encrypt
- Domain: cyber.nassau-records.de (API at api.cyber.nassau-records.de)
- 3 containers: PostgreSQL, notebook-server, notebook-frontend
- Verification checklist, rollback procedures
- **Status**: Fully specified, not deployed

---

## 5. Key Technical Details for Future Sessions

### Building and testing
```bash
cd notebook/
cargo build                          # Build all crates
cargo test                           # Run all unit tests (no DB required)
cargo clippy -- -D warnings          # Lint (CI treats warnings as errors)
cargo fmt --check                    # Format check
cargo run --bin notebook-server      # Start HTTP server (needs PostgreSQL)
cargo run --bin notebook -- help     # CLI help
```

### Database
- PostgreSQL 16 with Apache AGE extension for graph traversal
- `docker-compose -f notebook/docker-compose.yml up -d` starts the DB
- Migrations auto-run on server connect
- Tables: authors (32-byte Ed25519 identity), notebooks, notebook_access, entries (content as BYTEA, integration_cost as JSONB, references as UUID[])
- Sequence is unique per (notebook_id, sequence) pair

### Integration cost computation pipeline
1. Load/create CoherenceSnapshot for notebook
2. Capture cluster state BEFORE adding entry
3. Add entry: tokenize, compute TF-IDF vector, assign to best cluster (or create singleton)
4. Capture cluster state AFTER
5. Diff: count entries_revised (cluster reassignments), references_broken (cross-cluster), catalog_shift (cosine distance of merged cluster vectors), orphan (new singleton + no references)
6. Store entry with computed cost
7. Queue retroactive propagation job (async) for affected entries

### Known limitations of entropy engine (documented in integration-cost-summary.md)
- **Semantic blindness**: bag-of-words TF-IDF can't distinguish negation ("won't rain" ~ "will rain")
- **Non-text content**: falls back to topic keyword matching for non-text/* MIME types
- **Fixed clustering threshold**: not adaptive per cluster (global 0.3 threshold)
- **No LLM integration**: summaries are extractive, not generative

### CI/CD
- GitHub Actions: build, test, clippy, fmt, cargo doc
- Rust only, no integration tests with PostgreSQL in CI
- No frontend testing in CI

---

## 6. Open Questions for Future Claude Instances

### Architecture & Design

1. **Auth enforcement gap**: The server currently has NO authentication on any route. Plans 01-02 specify the full auth system but it's not implemented. Should this be the next priority before any new features?

2. **recent_entropy TODO**: `notebook-store/src/repository.rs` hardcodes `ActivityContext.recent_entropy = 0.0`. This is the only cross-crate integration gap. How should the entropy engine be wired into the store? Options: (a) pass engine reference to repository, (b) compute at server layer, (c) store snapshots in DB and query. The server's AppState already has both Repository and IntegrationCostEngine - is the wiring just missing in the route handlers?

3. **Docker-compose completeness**: The docker-compose.yml only defines the postgres service. The notebook-server and notebook-frontend services are referenced in Plan 04 but not present in the file. Is this intentional (deploy via Coolify) or an oversight?

4. **Bootstrap vs production server**: The bootstrap server (Python, port 8723) and the Rust server (port 3000) coexist. The orchestrator-instructions.md references the bootstrap at 8723. What's the migration path? When does the bootstrap get retired?

5. **CLAUDE.md accuracy**: CLAUDE.md says "edition 2024" but this should be verified against actual Cargo.toml. It also lists "five crates" but there are four crates + CLI. Minor, but future Claude instances should check.

### Entropy Engine

6. **Semantic understanding**: The TF-IDF bag-of-words approach is known to be semantically blind. Is there a plan to integrate embeddings (e.g., from a local model or API)? The project-plan.md mentions "lightweight embeddings" as an option in Phase 2.

7. **Catalog shift metric**: discussion.md lists this as open question #1. The current implementation uses cosine distance of merged cluster TF-IDF vectors. Is this capturing "the browse summary reorganized" well enough? Has anyone validated this against intuition?

8. **Orphan threshold**: Currently adaptive via Welford's algorithm (mean + 2*stddev). Is this the right statistical model? With <10 observations, falls back to 0.7. Has this been tested with real multi-agent workloads?

9. **Retroactive propagation**: The PropagationWorker exists but uses a NoOpCostUpdater. Is the actual CostUpdater that writes back to the database implemented? If not, retroactive cost updates are a no-op.

### Implementation Gaps

10. **Integration tests in CI**: The two_agent_exchange test requires a running server + database. There's no CI setup for this. Should there be a docker-compose-based CI job?

11. **Python client tests**: The Python client exists but are there any tests? `pyproject.toml` lists dev dependencies but no test files were found.

12. **MCP server testing**: notebook_mcp.py has no tests. It's a thin wrapper but still - has it been tested against a running server?

13. **Performance baselines**: project-plan.md specifies "500ms budget for integration cost on 10,000 entries" and "BROWSE within 1s". Have any benchmarks been run? The notebook-entropy crate has benchmark files (browse_latency.rs, graph_traversal.rs, search_performance.rs, write_throughput.rs) - what do they show?

### Production Readiness

14. **Secret management**: JWT_SECRET is passed via env var. No rotation mechanism. Is this acceptable for initial deployment?

15. **Rate limiting**: Not implemented anywhere. The bootstrap has none, the Rust server has none, and Plans 01-04 don't mention it. Needed before public deployment?

16. **Monitoring/observability**: tracing is used throughout the Rust code but there's no structured logging configuration, no metrics endpoint, no alerting. What's the observability plan?

17. **Backup/restore**: No tooling or documentation for database backups. The data in notebooks is intended to be an entity's persistent identity - losing it would be catastrophic.

18. **CORS default**: docker-compose sets CORS_ALLOWED_ORIGINS="*". Plan 04 restricts it for production. Is there a risk someone deploys with the default?

### Philosophical / Product

19. **Federation (Phase 6)**: The project-plan identifies this as future work with four open questions: discovery protocol, cross-notebook references, remote integration cost, trust model. Has any thinking progressed on these?

20. **Multi-modal integration cost**: The entropy engine only truly works for text/* content. For images, code, structured data - the topic-keyword fallback is crude. What's the long-term plan?

21. **Shared notebook semantics**: When two agents share a notebook, it "creates a group entity with its own evolving knowledge." But the current SHARE operation is just permission granting. Is there a concept of a notebook's collective identity beyond the sum of its entries?

22. **Validity/decay**: discussion.md mentions "validity notion (does this knowledge decay?)" as an entry axiom, but it's not implemented in the Entry type or anywhere in the codebase. Was this intentionally deferred or forgotten?

---

## 7. Suggested Reading Order for Future Claude Instances

1. **This file** (you're here)
2. `CLAUDE.md` - build commands and architecture overview
3. `notebook/docs/discussion.md` - philosophical foundation (essential context)
4. `notebook/docs/integration-cost-summary.md` - how entropy actually works
5. `notebook/docs/api-reference.md` - the API contract
6. `notebook/crates/notebook-entropy/src/engine.rs` - the heart of the system
7. `notebook/crates/notebook-core/src/types.rs` - domain types
8. `notebook/crates/notebook-server/src/routes/entries.rs` - write path
9. `notebook/docs/plans/` - upcoming work (01-04)

---

## 8. Summary Assessment

This is a **well-architected, philosophically grounded project** with a genuinely novel insight about entropy and knowledge integration. The Rust implementation is high-quality: strong types, comprehensive tests (100+), proper async, good error handling. The core platform (all 6 operations) is working end-to-end.

The main gaps are:
- **No authentication** on the production server (Plans 01-02 specify it)
- **No deployment** yet (Plan 04 specifies it)
- **No frontend** yet (Plan 03 specifies it)
- **Entropy engine semantic limitations** (TF-IDF only, no embeddings)
- **One cross-crate wiring TODO** (recent_entropy in ActivityContext)

The project is at the transition point between "core platform works" and "production-ready with auth and deployment." The next logical step is Plan 01 (auth), which unblocks Plan 02 (management), which unblocks Plan 03 (frontend), which unblocks Plan 04 (deployment).
