# Notebook Server v2 — Current Backend

This is the **current, production backend** for the Cyber knowledge exchange platform. It's built with .NET 10 and provides a comprehensive REST API for notebook operations, batch processing, search, and job management.

## Overview

- **Technology:** .NET 10, ASP.NET Core, Entity Framework Core
- **Database:** PostgreSQL with Apache AGE for graph queries
- **Status:** Active development and deployment
- **Version:** v2 (evolved from Rust v1)

## Directory Structure

```
backend/
├── src/
│   ├── Notebook.Server/              # ASP.NET Core HTTP API
│   │   ├── Endpoints/               # REST endpoints (entries, browse, jobs, etc.)
│   │   ├── Models/                  # Request/response types
│   │   ├── Auth/                    # Authentication middleware
│   │   └── Program.cs               # Startup configuration
│   ├── Notebook.Domain/              # Core domain types
│   │   └── Types/                   # Entry, Claim, Job, etc.
│   ├── Notebook.Data/                # EF Core persistence layer
│   │   ├── Repositories/            # Data access
│   │   └── Migrations/              # EF Core migrations
│   ├── Notebook.Entropy/             # Integration cost engine
│   └── Notebook.Services/            # Business logic
├── tests/
│   └── Notebook.Tests/              # Unit & integration tests
├── mcp/                             # MCP server integration
├── robots/                          # Worker scripts & utilities
├── docs/                            # Architecture documentation (moved to root docs/)
└── Notebook.sln                     # Solution file
```

## Quick Start

### Build
```bash
cd backend
dotnet build
```

### Run
```bash
cd backend/src/Notebook.Server
dotnet run
```

The API listens on `http://localhost:5201` (or configured port).

### Test
```bash
cd backend
dotnet test
```

## API Operations

**Six Core Operations:**
- **POST /notebooks/{id}/entries** — WRITE: Create entry
- **PUT /notebooks/{id}/entries/{eid}** — REVISE: Update entry
- **GET /notebooks/{id}/entries/{eid}** — READ: Retrieve entry
- **GET /notebooks/{id}/browse** — BROWSE: List entries with filtering
- **GET /notebooks/{id}/observe** — OBSERVE: Change feed
- **POST /notebooks/{id}/share** — SHARE: Grant access

**Additional Operations:**
- **POST /notebooks/{id}/batch** — Batch write multiple entries
- **GET /notebooks/{id}/search** — Full-text search
- **GET /jobs** — Query job queue
- **POST /notebooks/{id}/entries/{eid}/claims** — Manage claims

## Key Features

### Filtered Browse (`GET /notebooks/{id}/browse`)
Rich server-side filtering with parameters:
- `topic_prefix` — Hierarchical topic search
- `claims_status` — Filter by claim verification status
- `integration_status` — Filter by integration state
- `has_friction_above` — Filter by friction threshold
- `author` — Filter by entry author
- `sequence_min`, `sequence_max` — Filter by insertion order
- `needs_review` — Show flagged entries only
- `limit`, `offset` — Pagination

### Full-Text Search (`GET /notebooks/{id}/search`)
Tantivy-powered semantic indexing:
- Search all entry content
- Return relevance scores
- Extract snippets with context

### Batch Operations (`POST /notebooks/{id}/batch`)
Create multiple entries atomically:
- Single API call for multiple entries
- Atomic transaction
- Support for claims and source metadata

### Job Queue
Async processing for:
- Claim distillation
- Claim comparison
- Topic classification
- Embeddings (Ollama integration)

## Configuration

Edit `src/Notebook.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=thinktank;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Urls": "http://localhost:5201"
}
```

## Database

PostgreSQL with Apache AGE graph extension.

### Initialize
```bash
docker compose -f ../infrastructure/docker-compose.yml up -d
# Migrations run automatically on startup
```

### Manual Migration
```bash
cd backend/src
dotnet ef database update --project Notebook.Data
```

## Testing

```bash
# All tests
cd backend
dotnet test

# Specific category
dotnet test --filter "Category=Integration"

# With coverage
dotnet test /p:CollectCoverage=true
```

## Development

### Code Style
- C# 13 idioms (records, primary constructors, file-scoped namespaces)
- ASP.NET Core minimal APIs
- Entity Framework Core with raw SQL where needed
- System.Text.Json serialization

### Add a Package
```bash
cd backend
dotnet add [PROJECT] package [PACKAGE-NAME]
```

### Format Check
```bash
dotnet format --verify-no-changes
```

## Documentation

**Architecture & Design:**
See `../docs/architecture/` for detailed design documents:
- 00-OVERVIEW.md — System layers and cost model
- 03-SERVER-ENHANCEMENTS.md — New API surface
- 04-ROBOT-WORKERS.md — Async worker pools
- 05-INGEST-PIPELINE.md — Bulk data ingestion

**Feature Status:**
See `../docs/architecture/10-USER-FACING-FEATURES.md` for:
- What features are implemented
- What's in the frontend
- What's in the backend only

**Roadmap:**
See `../docs/roadmap/` for:
- Completed implementations
- Planned work
- Proposed features

## Frontend Integration

The frontend (Blazor at `../frontend/admin/`) connects to this backend via:
- Base URL: Configured in `appsettings.json`
- Authentication: JWT Bearer tokens
- API Client: `NotebookApiClient` in frontend

Frontend feature coverage: **13/16 domains (81%)**

## Legacy Backend

For reference, the original Rust v1 backend is at `../legacy/notebook/`.
This is kept for reference only and is not actively maintained.

## Troubleshooting

### Database Connection Failed
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Check logs
docker logs notebook-db
```

### Port Already in Use
Update `appsettings.json`:
```json
{
  "Urls": "http://localhost:5999"
}
```

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Related Links

- [Main README](../README.md)
- [Architecture Overview](../docs/ARCHITECTURE.md)
- [Setup & Development](../docs/SETUP.md)
- [Feature Matrix](../docs/architecture/10-USER-FACING-FEATURES.md)
- [Roadmap](../docs/roadmap/INDEX.md)

---

**Status:** Active Development
**Last Updated:** February 2026
