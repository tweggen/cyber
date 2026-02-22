# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Knowledge exchange platform that builds an externalized memory substrate for AI and biological entities. Notebooks enable persistent, evolving identity through shared entries with entropy-based knowledge integration metrics. The core insight: **integration cost (resistance to change) IS entropy**, providing a time arrow without clock synchronization.

See `legacy/notebook/docs/discussion.md` for the philosophical foundation and `legacy/notebook/docs/project-plan.md` for the implementation roadmap.

## Build & Development Commands

### .NET Frontend Admin Panel (run from `frontend/admin/`)

```bash
dotnet restore                       # Restore dependencies
dotnet build                         # Build project
dotnet run                           # Start development server (http://localhost:5000)
dotnet watch run                     # Hot reload on changes
dotnet format                        # Format code
```

### Rust Backend (run from `legacy/notebook/`)

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

### Python Client (run from `legacy/notebook/python/`)

```bash
pip install -e ".[dev]"              # Install with dev deps
pytest                               # Run tests
mypy notebook_client                 # Type check
ruff check notebook_client           # Lint
black notebook_client                # Format
```

### Database Migrations

```bash
# Admin panel database
psql -U postgres -f infrastructure/postgres/migrations/admin/000_create_admin_db.sql
psql -U postgres -d notebook_admin -f infrastructure/postgres/migrations/admin/022_admin_organization_quotas.sql

# Backend database
psql -U postgres -f infrastructure/postgres/migrations/init.sql
psql -U postgres -f infrastructure/postgres/migrations/server/000_create_thinktank_db.sql
psql -U postgres -d thinktank -f infrastructure/postgres/migrations/server/002_schema.sql
# ... (apply remaining migrations in order)
```

### Infrastructure

```bash
docker compose -f infrastructure/docker-compose.yml up -d      # Start PostgreSQL + Apache AGE
python3 legacy/notebook/bootstrap/bootstrap_notebook.py --port 8723 --data ./notebook-data  # Bootstrap server
```

## Architecture

### .NET Admin Panel (`frontend/admin/`)

**Blazor Server (.NET 10)** providing comprehensive system administration interface:

**Core Components:**
- **Pages/** — Routable pages (Users, Organizations, Quotas, Notebooks, Audit Trail)
- **Models/** — Data models and DTOs (ApplicationUser, UserQuota, OrganizationQuota, AuditLogEntry, etc.)
- **Services/** — Business logic layer:
  - **NotebookApiClient** — HTTP client for backend notebook API
  - **QuotaService** — User and organization quota management with inheritance
  - **UsageAggregationService** — Real-time usage statistics from notebook API
  - **CurrentUserService** — Authentication and authorization
- **Components/** — Reusable Blazor components and shared UI elements
- **Data/** — Entity Framework Core database context and models

**Database:**
- PostgreSQL (`notebook_admin` database)
- Separate from backend (thinktank) database
- Manages: Users, Quotas, Audit logs, Organization settings

**Phase 2 Features (Quota Monitoring):**
- Organization-level quota defaults (50 notebooks, 5000 entries, 10MB size, 1GB storage)
- Quota inheritance: User-specific → Organization → System defaults
- Usage progress bars on QuotaManagement.razor (notebooks, entries, storage)
- OrganizationQuota model and SQL migration

### Rust Backend (`legacy/notebook/`)

Five crates in a workspace, edition 2024:

- **notebook-core** — Domain types, Ed25519 crypto, identity. Every entry carries: content blob (representation-agnostic), content-type, cryptographic authorship, causal context (cyclic references allowed), and system-computed integration cost.
- **notebook-entropy** — Integration cost engine. TF-IDF similarity, agglomerative clustering, coherence snapshots, catalog generation with token budgets, Tantivy full-text search, retroactive cost propagation.
- **notebook-store** — PostgreSQL persistence via sqlx. Apache AGE for graph traversal of cyclic knowledge references. Migrations in `postgres/migrations/server/`.
- **notebook-server** — Axum HTTP API implementing six operations: WRITE, REVISE, READ, BROWSE, SHARE, OBSERVE. Stateless REST; entropy engine maintains in-memory coherence snapshots rebuilt from DB.
- **cli** — Clap-based CLI with subcommands matching the six operations plus delete, list, create.

### Python Client (`legacy/notebook/python/`)

Pure Python (3.9+) HTTP client wrapping all six operations. Types in dataclasses, custom error hierarchy.

### MCP Integration (`legacy/notebook/mcp/notebook_mcp.py`)

Model Context Protocol server exposing the six operations as tools for Claude Desktop. Authenticates via JWT Bearer token (`NOTEBOOK_TOKEN` env var).

## Key Design Decisions

### Backend (Notebook)

- **Causal positions, not timestamps**: Monotonic per-notebook sequence counter. No wall-clock dependency.
- **Representation-agnostic content**: JSONB/bytea blob with open MIME-type registry. The platform never interprets content.
- **Federated identity**: Ed25519 signatures, AuthorId derived from public key hash, no central PKI.
- **Two-phase integration**: New entries go through coherence check → cost computation → background retroactive propagation.
- **Token-budgeted catalog**: BROWSE returns cluster summaries constrained by token count (default 4000 ≈ 53 summaries), ordered by integration cost then stability.

### Admin Panel

- **Separate database**: Admin database (`notebook_admin`) is independent from backend (`thinktank`). Admin only stores external references to backend resources (OrganizationId, UserId, etc.).
- **No foreign keys to backend**: Organizations and users exist in backend; admin stores only IDs. This allows independent deployment and scaling.
- **SQL migrations for both databases**: `infrastructure/postgres/migrations/admin/` and `infrastructure/postgres/migrations/server/` keep migrations organized by database.
- **Inheritance-based quota model**: User quotas inherit from organization → system defaults without write-through (read-only inheritance).

## Project Status

### Admin Panel (Frontend)

**Current Phase: Phase 2 (Quota Monitoring)** ✅ COMPLETE

- **Phase 0** ✅ — Admin Shell with unified navigation, dashboard, user list/detail, quota management
- **Phase 1** ✅ — User search/filter, metadata (created date, last login), lock reason tracking, quota usage visualization
- **Phase 2** ✅ — Organization quota defaults, quota inheritance (User → Org → System), usage progress bars on quota edit

**Semantic Search UI** ✅ COMPLETE:
- Search mode toggle (Lexical/Semantic) on notebook view
- Embedding-based cosine similarity search via backend
- MCP tool: `thinktank_semantic_search`

**Phase 3+** (Planned):
- Email notifications for account events
- Bulk user operations
- Custom quota templates
- API rate limiting UI

### Backend Notebook (Legacy)

**Status:** Production-ready (Rust v1)

Foundation complete with production API:
- Core operations (WRITE, REVISE, READ, BROWSE, SHARE, OBSERVE) ✅
- Integration cost engine (entropy metrics) ✅
- Full-text search via Tantivy ✅
- Security model (clearances, compartments) ✅
- Python client and MCP integration ✅

See `legacy/notebook/docs/project-plan.md` for the full 7-phase Rust architecture roadmap.

### Database Migrations

**Organized by database:**
- `infrastructure/postgres/migrations/admin/` — Admin panel database (`notebook_admin`)
- `infrastructure/postgres/migrations/server/` — Backend notebook database (`thinktank`)
- `infrastructure/postgres/migrations/init.sql` — PostgreSQL extension setup (Apache AGE)
