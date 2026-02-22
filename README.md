# Cyber: Knowledge Exchange & Entropy-Based Integration

A platform for building externalized memory substrates that enable persistent, evolving identity through shared entries with entropy-based knowledge integration metrics.

**Core Insight:** Integration cost (resistance to change) IS entropy, providing a time arrow without clock synchronization.

---

## 📊 Admin Panel Status

**Current Phase: Phase 1 (User Management Enhancements)** ✅ COMPLETE

The admin panel provides a comprehensive management interface for system administrators:

### ✅ Phase 0: Admin Panel Shell (Complete)
- Unified admin navigation with role-based access
- Dashboard with activity summaries
- User list and detail pages with basic management
- Quota management interface
- Notebook browsing and management

### ✅ Phase 1: User Management Enhancements (Complete)
- **Search & Filtering** — Search by username/email/display name, filter by user type and lock status
- **User Metadata** — Track created date, last login, user type (Human, Service Account, Bot)
- **Lock Reason Tracking** — Document why accounts are locked for compliance/audit
- **Quota Usage Visualization** — Real-time progress bars showing resource utilization
- **Enhanced Lock Modal** — Lock accounts with predefined reasons and notes
- **Database Migration** — New columns and indexes for efficient queries

### 🔮 Phase 2: Planned Enhancements
- User batch import/export (CSV)
- Advanced audit filtering and reporting
- Email notifications for account events
- Bulk user operations
- Custom quota templates
- API rate limiting UI

---

## 📊 Backend Feature Coverage

**Frontend Implementation Status: 81% Complete** (13 of 16 feature domains fully implemented)

| Status | Count | Features |
|--------|:-----:|----------|
| ✅ Fully Implemented | 13 | Organizations, Groups, Security Clearances, Agent Management, Subscriptions, Audit Trail, Content Reviews, Full-Text Search, **Browse Filters**, Job Pipeline, Sharing, Group Access, Quotas |
| ⚠️ Partially Covered | 3 | Batch Entry Creation, Semantic Search UI, Notebook Classification |
| ❌ Not Supported | 0 | — |

For detailed feature documentation, see [USER-FACING-FEATURES.md](docs/architecture/10-USER-FACING-FEATURES.md)

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 (for frontend)
- Rust 1.80+ (for legacy backend)
- Docker & Docker Compose
- Python 3.9+ (for utilities)

### Frontend Setup (Current)

```bash
# Navigate to frontend
cd frontend/admin

# Restore dependencies and build
dotnet restore
dotnet build

# Run development server
dotnet run
# Open http://localhost:5000
```

### Backend Setup (Rust v1 - Production)

```bash
# Navigate to Rust backend workspace
cd legacy/notebook

# Build all crates
cargo build

# Run HTTP server
cargo run --bin notebook-server
# Listens on http://localhost:8723 by default
```

### MCP Servers for Claude Desktop

**Current Production MCP:**
```bash
# Rust-based MCP (production use)
cd legacy/notebook/mcp
python3 notebook_mcp.py
# Configure in Claude Desktop's claude_desktop_config.json
```

**Future MCP Servers (not yet in production):**
- `backend/mcp/thinktank_mcp.py` — MCP for .NET v2 backend (in development)
- `backend/mcp/wild_mcp.py` — Claims-aware retrieval MCP (in development)

### Database & Infrastructure

```bash
# Start PostgreSQL and Apache AGE
cd legacy/notebook
docker compose -f deploy/docker-compose.yml up -d

# Bootstrap notebook server with sample data
python3 bootstrap/bootstrap_notebook.py --port 8723 --data ./notebook-data
```

---

## 📁 Repository Structure

```
cyber/
├── README.md                        # This file
├── CLAUDE.md                        # Developer guidance (AI-friendly)
│
├── scripts/                         # Installation & utility scripts
│   ├── install.sh                   # Bash installation
│   ├── install.ps1                  # PowerShell installation
│   └── claude-add-notebook.sh        # Notebook CLI helper
│
├── infrastructure/                  # Deployment & database
│   ├── docker-compose.yml           # Current stack
│   ├── docker-compose.annotated.yml # Reference
│   ├── Dockerfile.backend           # Backend image
│   ├── Dockerfile.legacy            # Legacy backend image
│   └── postgres/
│       ├── init-thinktank.sh        # Database initialization
│       └── migrations/              # Database migrations
│
├── frontend/                        # .NET Blazor Server UI (current)
│   └── admin/
│       ├── Components/              # Blazor components
│       ├── Models/                  # DTOs and data models
│       ├── Services/                # API client, auth, token service
│       └── Pages/                   # Routable pages
│
├── backend/                         # .NET Backend v2 (in development)
│   ├── src/
│   │   ├── Notebook.Server/         # HTTP API (future replacement)
│   │   ├── Notebook.Domain/         # Core domain models
│   │   ├── Notebook.Data/           # PostgreSQL persistence
│   │   └── Notebook.Services/       # Business logic
│   ├── tests/                       # Integration & unit tests
│   ├── mcp/                         # MCP servers for .NET backend (future)
│   │   ├── thinktank_mcp.py         # Notebook interface (not in production)
│   │   └── wild_mcp.py              # Semantic search interface (not in production)
│   ├── robots/                      # Worker scripts
│   ├── README.md                    # Backend documentation
│   └── Notebook.sln                 # Solution file
│
├── docs/                            # Project documentation
│   ├── README.md                    # Documentation index
│   ├── SETUP.md                     # Setup & development guide
│   ├── ARCHITECTURE.md              # System architecture overview
│   ├── architecture/                # Detailed design documents
│   │   ├── 00-OVERVIEW.md           # System layers, design principles
│   │   ├── 01-CLAIM-REPRESENTATION.md
│   │   ├── 02-ENTROPY-AND-FRICTION.md
│   │   ├── 03-SERVER-ENHANCEMENTS.md
│   │   ├── 04-ROBOT-WORKERS.md
│   │   ├── 05-INGEST-PIPELINE.md
│   │   ├── 06-MIGRATION.md
│   │   ├── 07-NORMALIZATION.md
│   │   ├── 08-SECURITY-MODEL.md
│   │   ├── 09-PHASE-HUSH.md
│   │   ├── 10-USER-FACING-FEATURES.md ← Feature status (13/16 = 81%)
│   │   └── [other architecture docs]
│   └── roadmap/                     # Implementation roadmap (Kanban board)
│       ├── INDEX.md                 # Kanban overview
│       ├── proposed/                # Future feature ideas
│       ├── planned/                 # Approved for implementation
│       │   ├── 05-ROBOT-WORKERS.md
│       │   └── 06-MCP-UPDATES.md
│       └── done/                    # Completed implementations
│           ├── 01-SCHEMA-AND-TYPES.md
│           ├── 02-BATCH-WRITE-AND-CLAIMS-API.md
│           ├── 03-JOB-QUEUE.md
│           └── 04-FILTERED-BROWSE-AND-SEARCH.md
│
└── legacy/                          # Production backend & reference code
    └── notebook/                    # Rust v1 backend (PRODUCTION)
        ├── crates/                  # Workspace crates
        │   ├── notebook-core/       # Domain types & crypto
        │   ├── notebook-entropy/    # Integration cost engine
        │   ├── notebook-store/      # PostgreSQL persistence
        │   ├── notebook-server/     # Axum HTTP API (production)
        │   └── cli/                 # Command-line tool
        ├── python/                  # Python HTTP client
        ├── mcp/                     # Claude MCP integration (PRODUCTION)
        │   └── notebook_mcp.py      # Current production MCP for Claude Desktop
        ├── docs/                    # Architecture documentation
        ├── bootstrap/               # Data initialization
        ├── Cargo.toml               # Rust workspace manifest
        ├── deploy/                  # Docker & deployment configs
        └── README.md                # Rust backend documentation
```

---

## 📚 Key Documentation

### Architecture & Design
- **[00-OVERVIEW.md](docs/architecture/00-OVERVIEW.md)** — System architecture, design principles, layer breakdown
- **[02-ENTROPY-AND-FRICTION.md](docs/architecture/02-ENTROPY-AND-FRICTION.md)** — Semantic comparison model, integration cost
- **[08-SECURITY-MODEL.md](docs/architecture/08-SECURITY-MODEL.md)** — Authorization, clearances, compartments

### Implementation & Development
- **[10-USER-FACING-FEATURES.md](docs/architecture/10-USER-FACING-FEATURES.md)** — Complete feature inventory with implementation status
- **[CLAUDE.md](CLAUDE.md)** — Developer setup, commands, architectural decisions
- **[03-SERVER-ENHANCEMENTS.md](docs/architecture/03-SERVER-ENHANCEMENTS.md)** — Server APIs: batch write, filtered browse, search
- **[05-INGEST-PIPELINE.md](docs/architecture/05-INGEST-PIPELINE.md)** — Bulk content ingest workflows

### Operations & Scaling
- **[04-ROBOT-WORKERS.md](docs/architecture/04-ROBOT-WORKERS.md)** — Job queue, worker types, scaling strategies
- **[12-SUBSCRIPTION-ARCHITECTURE.md](docs/architecture/12-SUBSCRIPTION-ARCHITECTURE.md)** — Cross-notebook mirroring

---

## 🏗️ System Architecture

```
┌──────────────────────────────────────────────────┐
│  Admin UI (.NET Blazor Server)                   │
│  ┌────────────────────────────────────────────┐  │
│  │ Dashboard, Users, Quotas, Notebooks        │  │
│  │ Organizations, Groups, Audit Trail         │  │
│  │ (Phase 0-1: User management with search,   │  │
│  │  filtering, quotas visualization)          │  │
│  └────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────┤
│  Backend (Production: Rust v1)                   │
│  - Axum HTTP API (entries, notebooks, sharing)   │
│  - Integration cost engine (entropy metrics)     │
│  - Full-text search via Tantivy                  │
│  - MCP: notebook_mcp.py for Claude Desktop       │
├──────────────────────────────────────────────────┤
│  PostgreSQL + Apache AGE Graph DB                │
│  - Entry storage with metadata                   │
│  - Graph for cross-references & causal history   │
│  - Job queue, audit log                          │
│  - User management (accounts, quotas, locks)     │
└──────────────────────────────────────────────────┘

Note: .NET v2 backend (backend/src/Notebook.Server) is in development
as a future replacement for the Rust backend.
```

**Key Features:**
- 📔 **Notebooks** — Persistent knowledge collections with shared access
- 🔍 **Filtered Browse** — Rich server-side filtering (topic, status, friction, author, etc.)
- 🔎 **Full-Text Search** — Tantivy-powered semantic indexing
- 📊 **Entropy Metrics** — Integration cost and friction tracking
- 🔐 **Security** — Classification levels, compartments, clearances
- 👥 **Organizations** — Hierarchical group management
- 📋 **Audit Trail** — Complete action history with filtering
- 🤖 **Worker Queue** — Job distribution for LLM processing
- 👤 **User Management** — Search, filter, quota tracking, lock reasons (Phase 1)

---

## 🔧 Development Workflow

### Building

```bash
# Frontend
cd frontend/admin && dotnet build

# Production Backend (Rust)
cd legacy/notebook && cargo build

# Development Backend (.NET v2)
cd backend && dotnet build
```

### Testing

```bash
# Rust backend tests
cd legacy/notebook && cargo test

# Python client tests
cd legacy/notebook/python && pytest

# .NET backend tests (development)
cd backend && dotnet test
```

### Code Quality

```bash
# Rust backend linting & formatting
cd legacy/notebook
cargo clippy -- -D warnings
cargo fmt --check

# Python client
cd legacy/notebook/python && ruff check && black --check .

# .NET backend linting & formatting (development)
cd backend && dotnet format
```

---

## 📖 Common Tasks

### Add a New Feature
1. Verify backend API support in [10-USER-FACING-FEATURES.md](docs/architecture/10-USER-FACING-FEATURES.md)
2. Add frontend UI components to `frontend/admin/Components/`
3. Add API models to `frontend/admin/Models/NotebookModels.cs`
4. Add API methods to `frontend/admin/Services/NotebookApiClient.cs`
5. Update feature documentation with new status
6. Test and commit

### Check Feature Status
→ See [10-USER-FACING-FEATURES.md](docs/architecture/10-USER-FACING-FEATURES.md) for complete feature matrix

### Deploy
- Docker Compose: `docker compose -f infrastructure/docker-compose.yml up`
- See deployment docs for production configuration

---

## 🤝 Contributing

1. Read [CLAUDE.md](CLAUDE.md) for development guidance
2. Refer to [10-USER-FACING-FEATURES.md](docs/architecture/10-USER-FACING-FEATURES.md) for feature status
3. Follow the architecture patterns in existing code
4. Test thoroughly before committing
5. Update documentation for new features

---

## 📝 License

[Add your license information here]

---

## 🔗 Related Resources

- **Philosophy:** See `legacy/notebook/docs/discussion.md` for conceptual foundations
- **Project Plan:** See `legacy/notebook/docs/project-plan.md` (legacy Rust architecture)
- **Implementation Plan:** See `docs/roadmap/` for current feature roadmaps
- **Architecture Deep-Dives:** See `docs/architecture/` for detailed design documents

---

**Last Updated:** February 2026
**Status:** Active Development (v2 - .NET Backend)
