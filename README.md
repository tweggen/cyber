# Cyber: Knowledge Exchange & Entropy-Based Integration

A platform for building externalized memory substrates that enable persistent, evolving identity through shared entries with entropy-based knowledge integration metrics.

**Core Insight:** Integration cost (resistance to change) IS entropy, providing a time arrow without clock synchronization.

---

## 📊 Feature Coverage

**Frontend Implementation Status: 81% Complete** (13 of 16 feature domains fully implemented)

| Status | Count | Features |
|--------|:-----:|----------|
| ✅ Fully Implemented | 13 | Organizations, Groups, Security Clearances, Agent Management, Subscriptions, Audit Trail, Content Reviews, Full-Text Search, **Browse Filters**, Job Pipeline, Sharing, Group Access, Quotas |
| ⚠️ Partially Covered | 3 | Batch Entry Creation, Semantic Search UI, Notebook Classification |
| ❌ Not Supported | 0 | — |

For detailed feature documentation, see [USER-FACING-FEATURES.md](thinktank/docs/10-USER-FACING-FEATURES.md)

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

### Backend Setup (.NET v2 - Current)

```bash
# Navigate to backend
cd thinktank/src/Notebook.Server

# Build and run
dotnet build
dotnet run
# Listens on http://localhost:5201 by default
```

### Legacy Backend (Rust v1 - Reference Only)

```bash
# Navigate to legacy workspace
cd notebook

# Build all crates
cargo build

# Run HTTP server
cargo run --bin notebook-server
# Listens on http://localhost:3000
```

### Database & Infrastructure

```bash
# Start PostgreSQL and Apache AGE
cd notebook
docker compose -f deploy/docker-compose.yml up -d

# Bootstrap notebook server with sample data
python3 bootstrap/bootstrap_notebook.py --port 8723 --data ./notebook-data
```

---

## 📁 Repository Structure

```
cyber/
├── frontend/
│   └── admin/                    # .NET Blazor Server UI (current)
│       ├── Components/           # Blazor components
│       ├── Models/              # DTOs and data models
│       ├── Services/            # API client, auth, token service
│       └── Pages/               # Routable pages
│
├── thinktank/                   # .NET Backend v2 (current)
│   ├── src/
│   │   ├── Notebook.Server/     # HTTP API
│   │   ├── Notebook.Domain/     # Core domain models
│   │   ├── Notebook.Data/       # PostgreSQL persistence
│   │   └── Notebook.Services/   # Business logic
│   ├── docs/                    # Architecture & design docs
│   ├── tests/                   # Integration & unit tests
│   └── plan/                    # Implementation plans
│
├── notebook/                    # Rust v1 (legacy/reference)
│   ├── crates/                  # Workspace crates
│   │   ├── notebook-core/       # Domain types & crypto
│   │   ├── notebook-entropy/    # Integration cost engine
│   │   ├── notebook-store/      # PostgreSQL via sqlx
│   │   ├── notebook-server/     # Axum HTTP API
│   │   └── cli/                 # Command-line tool
│   ├── python/                  # Python HTTP client
│   ├── mcp/                     # Claude MCP integration
│   ├── deploy/                  # Docker & infrastructure
│   └── bootstrap/               # Data initialization
│
├── CLAUDE.md                    # Developer guidance (AI-friendly)
├── README.md                    # This file
└── [Other project files]
```

---

## 📚 Key Documentation

### Architecture & Design
- **[00-OVERVIEW.md](thinktank/docs/00-OVERVIEW.md)** — System architecture, design principles, layer breakdown
- **[02-ENTROPY-AND-FRICTION.md](thinktank/docs/02-ENTROPY-AND-FRICTION.md)** — Semantic comparison model, integration cost
- **[08-SECURITY-MODEL.md](thinktank/docs/08-SECURITY-MODEL.md)** — Authorization, clearances, compartments

### Implementation & Development
- **[10-USER-FACING-FEATURES.md](thinktank/docs/10-USER-FACING-FEATURES.md)** — Complete feature inventory with implementation status
- **[CLAUDE.md](CLAUDE.md)** — Developer setup, commands, architectural decisions
- **[03-SERVER-ENHANCEMENTS.md](thinktank/docs/03-SERVER-ENHANCEMENTS.md)** — Server APIs: batch write, filtered browse, search
- **[05-INGEST-PIPELINE.md](thinktank/docs/05-INGEST-PIPELINE.md)** — Bulk content ingest workflows

### Operations & Scaling
- **[04-ROBOT-WORKERS.md](thinktank/docs/04-ROBOT-WORKERS.md)** — Job queue, worker types, scaling strategies
- **[12-SUBSCRIPTION-ARCHITECTURE.md](thinktank/docs/12-SUBSCRIPTION-ARCHITECTURE.md)** — Cross-notebook mirroring

---

## 🏗️ System Architecture

```
┌──────────────────────────────────────────────────┐
│  Admin UI (.NET Blazor Server)                   │
│  - Notebook management, filtering, search        │
│  - Organization & group hierarchy                │
│  - Access control, audit trails                  │
│  - Agent & security management                   │
├──────────────────────────────────────────────────┤
│  .NET Backend Server (Notebook.Server)           │
│  - RESTful API (entries, notebooks, sharing)     │
│  - Full-text search via Tantivy                  │
│  - Batch operations, filtered browse             │
│  - Job queue for workers                         │
├──────────────────────────────────────────────────┤
│  PostgreSQL + Apache AGE Graph DB                │
│  - Entry storage with metadata                   │
│  - Graph for cross-references                    │
│  - Job queue, audit log                          │
└──────────────────────────────────────────────────┘
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

---

## 🔧 Development Workflow

### Building

```bash
# Frontend
cd frontend/admin && dotnet build

# Backend
cd thinktank/src/Notebook.Server && dotnet build

# Legacy (Rust)
cd notebook && cargo build
```

### Testing

```bash
# .NET tests
cd thinktank && dotnet test

# Rust tests
cd notebook && cargo test

# Python client tests
cd notebook/python && pytest
```

### Code Quality

```bash
# .NET linting & formatting
dotnet format

# Rust linting
cd notebook && cargo clippy -- -D warnings
cargo fmt --check

# Python
cd notebook/python && ruff check && black --check .
```

---

## 📖 Common Tasks

### Add a New Feature
1. Verify backend API support in [10-USER-FACING-FEATURES.md](thinktank/docs/10-USER-FACING-FEATURES.md)
2. Add frontend UI components to `frontend/admin/Components/`
3. Add API models to `frontend/admin/Models/NotebookModels.cs`
4. Add API methods to `frontend/admin/Services/NotebookApiClient.cs`
5. Update feature documentation with new status
6. Test and commit

### Check Feature Status
→ See [10-USER-FACING-FEATURES.md](thinktank/docs/10-USER-FACING-FEATURES.md) for complete feature matrix

### Deploy
- Docker Compose: `docker compose -f notebook/deploy/docker-compose.yml up`
- See deployment docs for production configuration

---

## 🤝 Contributing

1. Read [CLAUDE.md](CLAUDE.md) for development guidance
2. Refer to [10-USER-FACING-FEATURES.md](thinktank/docs/10-USER-FACING-FEATURES.md) for feature status
3. Follow the architecture patterns in existing code
4. Test thoroughly before committing
5. Update documentation for new features

---

## 📝 License

[Add your license information here]

---

## 🔗 Related Resources

- **Philosophy:** See `notebook/docs/discussion.md` for conceptual foundations
- **Project Plan:** See `notebook/docs/project-plan.md` (legacy Rust architecture)
- **Implementation Plan:** See `thinktank/plan/` for current feature roadmaps
- **Architecture Deep-Dives:** See `thinktank/docs/` for detailed design documents

---

**Last Updated:** February 2026
**Status:** Active Development (v2 - .NET Backend)
