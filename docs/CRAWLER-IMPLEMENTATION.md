# Crawler Implementation Guide

**Document Version**: 1.0
**Last Updated**: 2026-02-21
**Status**: Phase 1 Complete (Database Schema)

## Overview

This document describes the implementation of the crawler/ingest system for YourCyber. The system enables maintainers to mirror external data sources (Confluence, Git, FileSystem) into Thinktank with state tracking for incremental syncs.

## Phase 1: Database Schema & Configuration (✅ COMPLETE)

### Files Created

#### Database Migrations

1. **`infrastructure/postgres/migrations/018_crawlers.sql`**
   - Generic crawler metadata table
   - Crawler run history tracking
   - Indexes for performance

2. **`infrastructure/postgres/migrations/019_confluence_crawler_state.sql`**
   - Confluence-specific state storage
   - Configuration and incremental sync state
   - JSONB indexes for queries

#### JSON Schemas

1. **`backend/src/Notebook.Server/Schemas/confluence-crawler-config.schema.json`**
   - Configuration schema (required and optional fields)
   - Validation rules
   - Examples

2. **`backend/src/Notebook.Server/Schemas/confluence-crawler-sync-state.schema.json`**
   - Sync state schema (internal)
   - Page metadata structure
   - Examples

#### C# Validation Layer

1. **`backend/src/Notebook.Server/Services/Crawlers/CrawlerConfigValidator.cs`**
   - Schema validation class
   - Configuration parsing
   - Error reporting

2. **Updated `backend/src/Notebook.Server/Notebook.Server.csproj`**
   - Added `JsonSchema.Net` NuGet package
   - Embedded schema resources

### Database Schema Summary

#### crawlers table (Generic Metadata)

```
id                    UUID (PK)
notebook_id           UUID (FK)
name                  TEXT (unique with notebook_id)
source_type           TEXT (confluence|git|filesystem)
state_provider        TEXT (confluence_state|git_state|filesystem_state)
state_ref_id          UUID (FK to implementation-specific table)
is_enabled            BOOLEAN
schedule_cron         TEXT (NULL for manual)
last_sync_at          TIMESTAMPTZ
last_sync_status      TEXT (success|failed|partial|pending)
last_error            TEXT
created_at            TIMESTAMPTZ
updated_at            TIMESTAMPTZ
created_by            UUID (FK)
organization_id       UUID (FK)
```

**Indexes**:
- `notebook_id` — fast lookup by notebook
- `organization_id` — multi-tenant support
- `last_sync_at DESC NULLS LAST` — recent syncs first
- `source_type` — filtering by source
- `is_enabled` — only active crawlers

#### crawler_runs table (Execution History)

```
id                    UUID (PK)
crawler_id            UUID (FK)
started_at            TIMESTAMPTZ
completed_at          TIMESTAMPTZ
status                TEXT (running|success|failed|partial)
entries_created       INT
entries_updated       INT
entries_unchanged     INT
error_message         TEXT
stats                 JSONB {duration_ms, bytes_processed, pages_fetched, ...}
created_at            TIMESTAMPTZ
```

**Indexes**:
- `crawler_id` — all runs for a crawler
- `started_at DESC` — recent runs first
- `status` (partial) — failures and in-progress runs

#### confluence_crawler_state table (Confluence-Specific)

```
id                    UUID (PK)
config                JSONB (validated config)
sync_state            JSONB (incremental sync state)
created_at            TIMESTAMPTZ
updated_at            TIMESTAMPTZ
```

**Indexes**:
- JSONB path index on `config` for schema lookups
- Index on `sync_state->>'last_sync_timestamp'` for sync tracking

### Configuration Schema

Confluence configurations are validated against `confluence-crawler-config.schema.json`:

#### Required Fields

- `base_url` (string, URI) — Confluence instance URL
- `username` (string) — Confluence username or email
- `api_token` (string) — Confluence API token
- `space_key` (string, pattern: `^[A-Z][A-Z0-9]*$`) — Space key to crawl

#### Optional Fields

- `include_labels` (array of strings, default: []) — Only include pages with these labels
- `exclude_labels` (array of strings, default: []) — Exclude pages with these labels
- `max_pages` (integer, default: 0) — Max pages per sync (0 = unlimited)
- `include_attachments` (boolean, default: false) — Include attachments as entries

#### Example Configuration

```json
{
  "base_url": "https://company.atlassian.net/wiki",
  "username": "sarah@company.com",
  "api_token": "ATATT3xFfGF0...",
  "space_key": "ENG",
  "include_labels": ["published"],
  "exclude_labels": ["draft"],
  "max_pages": 1000,
  "include_attachments": false
}
```

### Sync State Schema

After each sync, the crawler stores incremental state:

```json
{
  "space_key": "ENG",
  "space_id": 12345,
  "last_sync_timestamp": "2026-02-21T10:00:00Z",
  "pages_synced": 42,
  "page_metadata": {
    "123": {
      "title": "API Documentation",
      "version": 5,
      "last_modified": "2026-02-20T15:30:00Z",
      "status": "current",
      "content_hash": "abc123def456..."
    }
  }
}
```

**Uses**:
- Incremental syncs — fetch only pages modified since `last_sync_timestamp`
- Duplicate detection — compare `content_hash` to skip unchanged pages
- Version tracking — detect Confluence page updates
- Audit trail — reconstruct sync history

## Running the Migrations

### Prerequisites

```bash
# Ensure PostgreSQL is running with thinktank database
docker compose -f infrastructure/docker-compose.yml up -d postgres
```

### Execute Migrations

```bash
# Using migrate tool
migrate -path infrastructure/postgres/migrations \
        -database "postgresql://user:pass@localhost:5432/thinktank" \
        up

# Or manually with psql
psql -d thinktank -f infrastructure/postgres/migrations/018_crawlers.sql
psql -d thinktank -f infrastructure/postgres/migrations/019_confluence_crawler_state.sql
```

### Verify Schema

```sql
-- Connect to thinktank database
\d crawlers
\d crawler_runs
\d confluence_crawler_state
```

Expected output:
```
                      Table "public.crawlers"
       Column       |           Type           | Collation
-------------------+--------------------------+-----------
 id                | uuid                     |
 notebook_id       | uuid                     |
 name              | text                     |
 source_type       | text                     |
 state_provider    | text                     |
 state_ref_id      | uuid                     |
 is_enabled        | boolean                  |
 schedule_cron     | text                     |
 last_sync_at      | timestamp with time zone |
 last_sync_status  | text                     |
 last_error        | text                     |
 created_at        | timestamp with time zone |
 updated_at        | timestamp with time zone |
 created_by        | uuid                     |
 organization_id   | uuid                     |
```

## C# Integration

### Using the Validator

```csharp
// In your service dependency injection
services.AddScoped<CrawlerConfigValidator>();

// In your controller/service
public class CrawlerService
{
    private readonly CrawlerConfigValidator _validator;

    public CrawlerService(CrawlerConfigValidator validator)
    {
        _validator = validator;
    }

    public async Task<Guid> ConfigureConfluenceCrawlerAsync(
        Guid notebookId,
        string configJson)
    {
        // Validate configuration
        _validator.ValidateConfluenceConfig(configJson);

        // Parse validated config
        var config = ConfluenceConfig.FromJson(configJson, _validator);

        // Save to database
        var state = new ConfluenceCrawlerState { Config = configJson };
        var stateId = await _context.ConfluenceCrawlerStates.AddAsync(state);
        await _context.SaveChangesAsync();

        var crawler = new Crawler
        {
            NotebookId = notebookId,
            Name = $"Confluence:{config.SpaceKey}",
            SourceType = "confluence",
            StateProvider = "confluence_state",
            StateRefId = stateId.Entity.Id,
            CreatedBy = currentUserId,
            OrganizationId = currentOrgId
        };

        await _context.Crawlers.AddAsync(crawler);
        await _context.SaveChangesAsync();

        return crawler.Id;
    }
}
```

### Parsing Configuration

```csharp
var config = ConfluenceConfig.FromJson(configJson, validator);
Console.WriteLine($"Crawling {config.SpaceKey} at {config.BaseUrl}");
Console.WriteLine($"Auth: {config.Username}");
Console.WriteLine($"Max pages: {config.MaxPages}");
```

### Working with Sync State

```csharp
var state = ConfluenceSyncState.FromJson(syncStateJson, validator);
Console.WriteLine($"Last sync: {state.LastSyncTimestamp}");
Console.WriteLine($"Pages tracked: {state.PageMetadata.Count}");

// Update after sync
state.LastSyncTimestamp = DateTime.UtcNow;
state.PagesSynced = newPages.Count;
var updatedJson = state.ToJson();
```

## What's Next: Phase 2-5

### Phase 2: ConfluenceCrawler Implementation
- Confluence REST API client
- Page fetching with pagination
- Label filtering
- Content hash computation
- Incremental state tracking
- Page→Entry conversion

### Phase 3: CrawlerService API
- Save/update crawler configuration
- Run crawler (sync/async)
- Fetch run history
- Test connection endpoint

### Phase 4: YourCyber UI
- Crawler configuration page (JSON editor)
- Run history dashboard
- Test connection button

### Phase 5: Testing & Documentation
- Integration tests
- User documentation
- Extension documentation

## File Locations

### Migrations
- `infrastructure/postgres/migrations/018_crawlers.sql`
- `infrastructure/postgres/migrations/019_confluence_crawler_state.sql`

### Schemas
- `backend/src/Notebook.Server/Schemas/confluence-crawler-config.schema.json`
- `backend/src/Notebook.Server/Schemas/confluence-crawler-sync-state.schema.json`

### Implementation
- `backend/src/Notebook.Server/Services/Crawlers/CrawlerConfigValidator.cs`

### Project Files
- `backend/src/Notebook.Server/Notebook.Server.csproj` (updated with JsonSchema.Net)

## References

- [07-CONFLUENCE-CRAWLER.md](roadmap/planned/07-CONFLUENCE-CRAWLER.md) — Full implementation plan
- [05-INGEST-PIPELINE.md](architecture/05-INGEST-PIPELINE.md) — Ingest architecture
- [04-ROBOT-WORKERS.md](architecture/04-ROBOT-WORKERS.md) — Job queue system

## Security Notes

1. **API Token Storage**: Store encrypted in database or use external secrets manager
2. **Configuration**: Never log sensitive config fields (api_token, credentials)
3. **Source Attribution**: All entries tagged with source and original URL
4. **Access Control**: Crawlers inherit notebook access control
