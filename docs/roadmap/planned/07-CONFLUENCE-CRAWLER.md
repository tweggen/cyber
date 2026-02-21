# 07-CONFLUENCE-CRAWLER

**Status**: Planned
**Priority**: High
**Effort**: Medium (3-4 sprints)
**Owner**: TBD
**Last Updated**: 2026-02-21

## Overview

Implement a Confluence crawler as the first source-specific ingest mechanism for YourCyber. This enables maintainers to mirror Confluence spaces into the Thinktank database with state tracking for incremental syncs.

This is **Phase 1** of the broader crawler/ingest architecture that will support Confluence, Git, and FileSystem sources.

## Architecture

### System Components

```
YourCyber (Admin UI)
    ↓ (JSON config)
CrawlerService (HTTP API)
    ↓ (enqueues job)
Job Queue (existing)
    ↓ (executes)
CrawlerWorker (process)
    ↓ (uses)
ConfluenceCrawler (implementation)
    ↓ (REST API calls)
Confluence Instance
    ↓ (fetches pages)
Batch Write API
    ↓ (submits)
Notebook Database (Thinktank)
```

### Generic vs Implementation-Specific State

**Generic State** (crawlers table):
- `id`, `notebook_id`, `name`, `source_type`
- `state_provider`, `state_ref_id` (FK to implementation table)
- `is_enabled`, `schedule_cron`, `last_sync_at`, `last_sync_status`
- `created_at`, `updated_at`, `created_by`, `organization_id`

**Confluence-Specific State** (confluence_crawler_state table):
- `config` (JSONB) — user-provided Confluence configuration
- `sync_state` (JSONB) — incremental sync tracking (page metadata, hashes, timestamps)

This separation allows evolution of each crawler independently without touching the generic schema.

## Database Schema

### crawlers table (generic)

```sql
CREATE TABLE crawlers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    name TEXT NOT NULL,
    source_type TEXT NOT NULL, -- 'confluence' | 'git' | 'filesystem'

    -- Implementation-specific state reference
    state_provider TEXT NOT NULL, -- 'confluence_state' | 'git_state' | 'filesystem_state'
    state_ref_id UUID NOT NULL,

    -- Generic tracking
    is_enabled BOOLEAN DEFAULT true,
    schedule_cron TEXT, -- NULL for manual, or '0 0 * * *' for scheduled
    last_sync_at TIMESTAMPTZ,
    last_sync_status TEXT, -- 'success' | 'failed' | 'partial' | 'pending'
    last_error TEXT,

    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now(),
    created_by UUID NOT NULL REFERENCES auth_users(id),
    organization_id UUID NOT NULL REFERENCES organizations(id),

    CONSTRAINT valid_source_type CHECK (source_type IN ('confluence', 'git', 'filesystem'))
);

CREATE INDEX idx_crawlers_notebook_id ON crawlers(notebook_id);
CREATE INDEX idx_crawlers_organization_id ON crawlers(organization_id);
CREATE INDEX idx_crawlers_last_sync_at ON crawlers(last_sync_at);
```

### crawler_runs table

```sql
CREATE TABLE crawler_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    crawler_id UUID NOT NULL REFERENCES crawlers(id) ON DELETE CASCADE,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    status TEXT NOT NULL, -- 'running' | 'success' | 'failed' | 'partial'
    entries_created INT DEFAULT 0,
    entries_updated INT DEFAULT 0,
    entries_unchanged INT DEFAULT 0,
    error_message TEXT,
    stats JSONB, -- {duration_ms, bytes_processed, pages_fetched, etc.}

    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_crawler_runs_crawler_id ON crawler_runs(crawler_id);
CREATE INDEX idx_crawler_runs_started_at ON crawler_runs(started_at DESC);
```

### confluence_crawler_state table

```sql
CREATE TABLE confluence_crawler_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Configuration (validated against JSON schema)
    config JSONB NOT NULL,

    -- Incremental sync state
    sync_state JSONB DEFAULT '{}',

    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);
```

## Configuration Schema

User-provided JSON configuration for Confluence crawlers.

### Required Fields

- `base_url` (string) — Confluence instance URL
  Example: `https://company.atlassian.net/wiki`

- `username` (string) — Confluence username or email
  Example: `user@company.com`

- `api_token` (string) — Confluence API token
  Example: `ATATT3xFfGF0...`
  **Security**: Store securely (encrypted in DB or secrets store)

- `space_key` (string) — Confluence space key to crawl
  Example: `ENG`

### Optional Fields

- `include_labels` (array of strings) — Only include pages with these labels
  Default: `[]` (all pages)
  Example: `["published", "current"]`

- `exclude_labels` (array of strings) — Exclude pages with these labels
  Default: `[]`
  Example: `["draft", "archived"]`

- `max_pages` (integer) — Maximum pages per sync
  Default: `0` (unlimited)

- `include_attachments` (boolean) — Include page attachments as separate entries
  Default: `false`

### Example Configuration

```json
{
  "base_url": "https://company.atlassian.net/wiki",
  "username": "sarah@company.com",
  "api_token": "ATATT3xFfGF0...",
  "space_key": "ENG",
  "include_labels": ["published"],
  "exclude_labels": ["draft", "deprecated"],
  "max_pages": 1000,
  "include_attachments": false
}
```

## Sync State Structure

After each successful sync, the crawler stores state for incremental updates:

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
    },
    "456": {
      "title": "Architecture Guide",
      "version": 3,
      "last_modified": "2026-02-19T10:00:00Z",
      "status": "current",
      "content_hash": "xyz789..."
    }
  }
}
```

**Uses**:
- **Incremental syncs** — fetch only pages modified since `last_sync_timestamp`
- **Duplicate detection** — compare `content_hash` to skip unchanged pages
- **Version tracking** — detect when Confluence page version increases
- **Audit trail** — reconstruct what was synced and when

## Implementation Plan

### Phase 1: Database & Schemas

**Tasks**:
1. Create migration for `crawlers` table (generic metadata)
2. Create migration for `crawler_runs` table (run history)
3. Create migration for `confluence_crawler_state` table
4. Add indexes for performance
5. Write JSON schema validation for Confluence config

**Deliverables**:
- DB schema in place
- JSON schema document
- Migration scripts

### Phase 2: ConfluenceCrawler Implementation

**Tasks**:
1. Implement `ConfluenceCrawler` class with:
   - Confluence REST API client (HTTP + Basic Auth)
   - Page fetching with pagination
   - Label filtering (include/exclude)
   - Content hash computation (SHA256)
   - Incremental state tracking
   - Page→Entry conversion
2. Implement content filtering pipeline:
   - Apply Wikipedia content filter to Confluence HTML
   - Convert to markdown
   - Add source attribution metadata
3. Write unit tests for crawler logic

**Deliverables**:
- `ConfluenceCrawler.cs` class
- Supporting DTOs (ConfluenceSpaceInfo, ConfluencePage, etc.)
- Unit tests

### Phase 3: CrawlerService API

**Tasks**:
1. Implement `CrawlerService` class with:
   - Save/update crawler configuration
   - Run crawler synchronously or async via job queue
   - Fetch crawler state and run history
   - Test connection endpoint
2. Implement `CrawlersController` with endpoints:
   - `POST /api/crawlers/{notebookId}/confluence` — configure
   - `POST /api/crawlers/{notebookId}/confluence/test` — test connection
   - `POST /api/crawlers/{notebookId}/confluence/run` — start sync
   - `GET /api/crawlers/{notebookId}/runs` — view run history
3. Integrate with existing Job Queue and Batch Write API

**Deliverables**:
- `CrawlerService.cs`
- `CrawlersController.cs`
- Integration tests

### Phase 4: YourCyber UI (JSON-based Config)

**Tasks**:
1. Create Blazor page: `/Pages/Crawlers/ConfigureCrawler.razor`
   - Notebook selection dropdown
   - JSON textarea with syntax highlighting
   - Save / Test Connection / Run Now buttons
   - Validation and error display
   - Example configuration collapsible
2. Create Blazor page: `/Pages/Crawlers/CrawlerRuns.razor`
   - View run history (status, pages fetched, entries created, timestamps)
   - Link to entries created in notebook
3. Add crawler management to navigation

**Deliverables**:
- UI pages (Blazor components)
- Client-side JSON validation
- Run history dashboard

### Phase 5: Testing & Documentation

**Tasks**:
1. Integration test against real Confluence instance (or mock)
2. Test incremental sync (verify unchanged pages not re-ingested)
3. Test error handling (invalid token, space not found, etc.)
4. Document configuration for end-users
5. Document extension points for adding new crawler types

**Deliverables**:
- Integration test suite
- User documentation
- Developer documentation

## Key Design Decisions

### 1. JSON Configuration vs UI Dialogs

**Decision**: Start with JSON text field, no specific UI dialogs yet.

**Rationale**:
- Faster to implement (no dialog blocker)
- More flexible (users can see all options)
- Mirrors infrastructure-as-code patterns (Kubernetes, Terraform)
- Easy to evolve — upgrade to specific dialogs later without breaking existing configs

### 2. Incremental Sync via Content Hashing

**Decision**: Track page version numbers AND content hash to detect changes.

**Rationale**:
- Confluence API rate limits favor pagination; hashing avoids unnecessary fetches
- Detects content changes even if version number doesn't increment
- Allows skipping unchanged pages → reduces notebook entry creation

### 3. State Storage in JSONB

**Decision**: Store all Confluence-specific state in `confluence_crawler_state.sync_state` JSONB field.

**Rationale**:
- No need for normalized tables for page metadata
- Flexible schema evolution (add new fields without migrations)
- Simple to query via JSONB operators
- Can be extended later if needed

### 4. Content Filter Pipeline

**Decision**: Apply Wikipedia content filter to Confluence pages (HTML→Markdown→Clean).

**Rationale**:
- Reuses existing filter infrastructure
- Confluence exports to HTML; filter cleans boilerplate
- Consistent with other sources

## Security Considerations

1. **API Token Storage**
   - Store encrypted in DB or use external secrets manager
   - Never log or expose in responses
   - Support rotating tokens without re-entering config

2. **Workstation-Based Execution**
   - Crawlers run in user's security context (can access Confluence via their network)
   - Job queue doesn't store sensitive config; only references it
   - Consider air-gapped/restricted networks

3. **Source Attribution**
   - Every entry tagged with source and original URL
   - Audit trail of what was crawled when
   - Support for deduplication/merging entries from same source

## Success Criteria

- ✅ Configuration saved and validated
- ✅ Confluence REST API successfully queried with user credentials
- ✅ Pages fetched and converted to markdown entries
- ✅ Incremental sync works (unchanged pages skipped)
- ✅ Content filtering applied (boilerplate removed)
- ✅ Entries created in Thinktank database
- ✅ Run history tracked in `crawler_runs` table
- ✅ YourCyber UI allows start/test/monitor crawls
- ✅ All unit and integration tests passing

## Open Questions

1. **Concurrent Crawlers**: Should multiple crawlers for the same notebook run sequentially or in parallel?
   - Concern: Rate limiting, entry deduplication
   - Decision pending

2. **Scheduled Execution**: Should we support cron-based scheduling or always manual + external scheduler (CI/CD)?
   - Current: Only manual trigger via CrawlerService.RunCrawlerAsync
   - Future: Could add `schedule_cron` field and background job to execute

3. **Confluence Attachments**: Should we support attachment ingestion?
   - Current: `include_attachments: false` in config (stubbed)
   - Future: Download attachments and store as separate notebook entries

4. **Error Recovery**: If sync fails midway, should we rollback entries or keep partial results?
   - Current: Log error, retry on next manual run
   - Future: Consider transaction semantics with notebook batch API

## Dependencies

- ✅ Job Queue infrastructure (existing)
- ✅ Batch Write API (existing)
- ✅ PostgreSQL with UUID support (existing)
- ✅ Content filter infrastructure (existing, enhanced in PR #6f93ed0)
- ✅ YourCyber (admin UI framework)

## Related Documents

- `04-ROBOT-WORKERS.md` — Job queue and worker architecture
- `05-INGEST-PIPELINE.md` — Bulk content ingest workflows
- `03-SERVER-ENHANCEMENTS.md` — Batch write API specification
- `06-CONFLUENCE-CRAWLER.md` (this document) — Confluence-specific implementation
- `07-GIT-CRAWLER.md` (future) — Git source crawler
- `08-FILESYSTEM-CRAWLER.md` (future) — FileSystem source crawler

## Appendix: DTO Definitions

```csharp
public class ConfluenceCrawlerConfig
{
    public string BaseUrl { get; set; }
    public string Username { get; set; }
    public string ApiToken { get; set; }
    public string SpaceKey { get; set; }
    public List<string> IncludeLabels { get; set; } = new();
    public List<string> ExcludeLabels { get; set; } = new();
    public int MaxPages { get; set; } = 0;
    public bool IncludeAttachments { get; set; } = false;
}

public class ConfluenceSyncState
{
    public string SpaceKey { get; set; }
    public long? SpaceId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public int PagesSynced { get; set; }
    public Dictionary<string, ConfluencePageMetadata> PageMetadata { get; set; }
}

public class ConfluencePageMetadata
{
    public string Title { get; set; }
    public int Version { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; }
    public string ContentHash { get; set; }
}

public class CrawlerRunResult
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "success";
    public int EntriesCreated { get; set; }
    public int EntriesUpdated { get; set; }
    public string ErrorMessage { get; set; }
    public object Stats { get; set; }
}
```
