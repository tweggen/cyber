# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Knowledge exchange platform that builds an externalized memory substrate for AI and biological entities. Notebooks enable persistent, evolving identity through shared entries with entropy-based knowledge integration metrics. The core insight: **integration cost (resistance to change) IS entropy**, providing a time arrow without clock synchronization.

See `notebook/docs/discussion.md` for the philosophical foundation and `notebook/docs/project-plan.md` for the implementation roadmap.

## Build & Development Commands

### Rust (run from `notebook/`)

```bash
cargo build                          # Build all crates
cargo test                           # Run all tests
cargo test -p notebook-entropy       # Test single crate
cargo clippy -- -D warnings          # Lint
cargo fmt --check                    # Check formatting
cargo bench                          # Run benchmarks (notebook-entropy)
cargo run --bin notebook-server      # Start HTTP server
cargo run --bin notebook -- help     # CLI help
```

### Python (run from `notebook/python/`)

```bash
pip install -e ".[dev]"              # Install with dev deps
pytest                               # Run tests
mypy notebook_client                 # Type check
ruff check notebook_client           # Lint
black notebook_client                # Format
```

### Infrastructure

```bash
docker compose -f deploy/docker-compose.yml up -d      # Start PostgreSQL + Apache AGE
python3 notebook/bootstrap/bootstrap_notebook.py --port 8723 --data ./notebook-data  # Bootstrap server
```

## Architecture

### Rust Workspace (`notebook/`)

Five crates in a workspace, edition 2024:

- **notebook-core** — Domain types, Ed25519 crypto, identity. Every entry carries: content blob (representation-agnostic), content-type, cryptographic authorship, causal context (cyclic references allowed), and system-computed integration cost.
- **notebook-entropy** — Integration cost engine. TF-IDF similarity, agglomerative clustering, coherence snapshots, catalog generation with token budgets, Tantivy full-text search, retroactive cost propagation.
- **notebook-store** — PostgreSQL persistence via sqlx. Apache AGE for graph traversal of cyclic knowledge references. Migrations in `postgres/migrations/`.
- **notebook-server** — Axum HTTP API implementing six operations: WRITE, REVISE, READ, BROWSE, SHARE, OBSERVE. Stateless REST; entropy engine maintains in-memory coherence snapshots rebuilt from DB.
- **cli** — Clap-based CLI with subcommands matching the six operations plus delete, list, create.

### Python Client (`notebook/python/`)

Pure Python (3.9+) HTTP client wrapping all six operations. Types in dataclasses, custom error hierarchy.

### MCP Integration (`notebook/mcp/notebook_mcp.py`)

Model Context Protocol server exposing the six operations as tools for Claude Desktop. Authenticates via JWT Bearer token (`NOTEBOOK_TOKEN` env var).

## Key Design Decisions

- **Causal positions, not timestamps**: Monotonic per-notebook sequence counter. No wall-clock dependency.
- **Representation-agnostic content**: JSONB/bytea blob with open MIME-type registry. The platform never interprets content.
- **Federated identity**: Ed25519 signatures, AuthorId derived from public key hash, no central PKI.
- **Two-phase integration**: New entries go through coherence check → cost computation → background retroactive propagation.
- **Token-budgeted catalog**: BROWSE returns cluster summaries constrained by token count (default 4000 ≈ 53 summaries), ordered by integration cost then stability.

## Project Status

Currently in early phases. Foundation types, workspace scaffolding, database schema, crypto primitives, Python client, MCP integration, and HTTP server framework are in place. The integration cost engine (Phase 2) is the highest-risk component. See `notebook/docs/project-plan.md` for the full 7-phase roadmap.
