# Thinktank v2 Implementation Plan

## Purpose

Step-by-step implementation plan for evolving the notebook server from v1 (40 entries, LLM-only MCP access) to v2 (100K+ entries, bulk ingest, cheap robot workers, targeted expensive LLM).

Each document in this directory is a self-contained implementation unit suitable for a Claude Code session. Work them in order — later steps depend on earlier ones.

## Technology Stack

- **C# / .NET 9** — ASP.NET Core minimal APIs
- **Entity Framework Core 9** + **Npgsql** — PostgreSQL ORM / data access
- **System.Text.Json** — JSON serialization
- **xUnit** + **FluentAssertions** — Testing
- **PostgreSQL** — Database (same schema as before)

## Repository Layout (relevant paths)

```
notebook/
  Notebook.sln                                      # Solution file
  src/
    Notebook.Core/                                   # Domain types (Entry, Claim, etc.)
      Types/
        Entry.cs
        Claim.cs
        ClaimComparison.cs
        Job.cs
    Notebook.Data/                                   # EF Core data access layer
      NotebookDbContext.cs
      Repositories/
        EntryRepository.cs
        JobRepository.cs
      Migrations/
    Notebook.Entropy/                                # Integration cost engine
      EntropyEngine.cs
    Notebook.Server/                                 # ASP.NET Core web API
      Program.cs
      Endpoints/
        EntryEndpoints.cs                            # WRITE/REVISE/READ
        BrowseEndpoints.cs                           # BROWSE
        BatchEndpoints.cs                            # Batch write (new)
        ClaimsEndpoints.cs                           # Claims update (new)
        JobEndpoints.cs                              # Job queue (new)
        SearchEndpoints.cs                           # Search (new)
      Auth/
        AuthorIdentityMiddleware.cs
      Models/
        Requests.cs
        Responses.cs
  tests/
    Notebook.Tests/                                  # Unit and integration tests
  migrations/                                        # Raw SQL migrations (fallback)
  python/notebook_client/                            # Python HTTP client
  cli/                                               # CLI tool
mcp/notebook_mcp.py                                  # MCP server for Claude Desktop
thinktank/docs/                                      # Design documents (the spec)
```

## Implementation Steps

| Step | Document | What | Depends On |
|------|----------|------|------------|
| 1 | `01-SCHEMA-AND-TYPES.md` | DB migration + C# types for claims, fragments, comparisons, jobs | Nothing |
| 2 | `02-BATCH-WRITE-AND-CLAIMS-API.md` | Batch write endpoint + claims storage endpoint | Step 1 |
| 3 | `03-JOB-QUEUE.md` | Job queue DB table, API endpoints, auto-creation triggers | Step 1 |
| 4 | `04-FILTERED-BROWSE-AND-SEARCH.md` | Enhanced BROWSE filters + new search endpoint | Step 1 |
| 5 | `05-ROBOT-WORKERS.md` | Python robot scripts (distill, compare, classify) | Steps 2, 3 |
| 6 | `06-MCP-UPDATES.md` | Expose new operations through MCP server | Steps 2, 3, 4 |

Steps 2, 3, and 4 can be worked in parallel once Step 1 is complete.
Steps 5 and 6 can be worked in parallel once their dependencies are done.

## Key Constraints

- **All v1 operations must continue working unchanged.** WRITE, REVISE, READ, BROWSE, OBSERVE, SHARE via MCP are untouched.
- **.NET 9 / C# 13.** Use current C# idioms — records, primary constructors, file-scoped namespaces, nullable reference types.
- **ASP.NET Core minimal APIs.** Endpoints use `MapGet`/`MapPost` patterns with typed parameter binding.
- **EF Core 9 + Npgsql.** Data access via EF Core with raw SQL where needed (e.g., `FOR UPDATE SKIP LOCKED`). Migrations via EF Core tooling.
- **Existing auth model.** JWT Bearer + optional `X-Author-Id` header (dev mode). New endpoints follow the same `AuthorIdentity` pattern via middleware or `IEndpointFilter`.
- **System.Text.Json.** All request/response types use `[JsonPropertyName]` attributes or configured `JsonSerializerOptions` with `camelCase` naming.

## Testing Strategy

Each step includes specific test commands. The general approach:

```bash
# From solution root:
dotnet build                                        # Compile check
dotnet test                                         # Unit + integration tests
dotnet format --verify-no-changes                   # Format check

# Integration tests require a running PostgreSQL instance:
docker-compose -f notebook/docker-compose.yml up -d
dotnet test --filter "Category=Integration"
```

## Design Documents Reference

The full design spec is in `thinktank/docs/`:
- `00-OVERVIEW.md` — Architecture layers and cost model
- `01-CLAIM-REPRESENTATION.md` — Claim data model, fragmentation, distillation
- `02-ENTROPY-AND-FRICTION.md` — Semantic comparison model
- `03-SERVER-ENHANCEMENTS.md` — All new API surface
- `04-ROBOT-WORKERS.md` — Stateless cheap-LLM workers
- `05-INGEST-PIPELINE.md` — End-to-end bulk ingest flow
- `06-MIGRATION.md` — v1 to v2 migration steps
