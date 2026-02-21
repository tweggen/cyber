-- Crawler infrastructure: generic metadata for all source crawlers.
-- Supports Confluence, Git, FileSystem sources with pluggable state storage.

CREATE TABLE IF NOT EXISTS crawlers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    source_type TEXT NOT NULL
        CHECK (source_type IN ('confluence', 'git', 'filesystem')),

    -- Implementation-specific state reference
    state_provider TEXT NOT NULL, -- 'confluence_state' | 'git_state' | 'filesystem_state'
    state_ref_id UUID NOT NULL,

    -- Configuration and tracking
    is_enabled BOOLEAN NOT NULL DEFAULT true,
    schedule_cron TEXT, -- NULL = manual, '0 0 * * *' = daily at midnight
    last_sync_at TIMESTAMPTZ,
    last_sync_status TEXT
        CHECK (last_sync_status IS NULL OR last_sync_status IN ('success', 'failed', 'partial', 'pending')),
    last_error TEXT,

    -- Audit trail
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL REFERENCES auth_users(id),
    organization_id UUID NOT NULL REFERENCES organizations(id),

    UNIQUE(notebook_id, name)
);

CREATE INDEX idx_crawlers_notebook_id ON crawlers(notebook_id);
CREATE INDEX idx_crawlers_organization_id ON crawlers(organization_id);
CREATE INDEX idx_crawlers_last_sync_at ON crawlers(last_sync_at DESC NULLS LAST);
CREATE INDEX idx_crawlers_source_type ON crawlers(source_type);
CREATE INDEX idx_crawlers_is_enabled ON crawlers(is_enabled) WHERE is_enabled = true;

-- Crawler execution history
CREATE TABLE IF NOT EXISTS crawler_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    crawler_id UUID NOT NULL REFERENCES crawlers(id) ON DELETE CASCADE,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    status TEXT NOT NULL DEFAULT 'running'
        CHECK (status IN ('running', 'success', 'failed', 'partial')),
    entries_created INT DEFAULT 0,
    entries_updated INT DEFAULT 0,
    entries_unchanged INT DEFAULT 0,
    error_message TEXT,
    stats JSONB, -- {duration_ms, bytes_processed, pages_fetched, etc.}

    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_crawler_runs_crawler_id ON crawler_runs(crawler_id);
CREATE INDEX idx_crawler_runs_started_at ON crawler_runs(started_at DESC);
CREATE INDEX idx_crawler_runs_status ON crawler_runs(status) WHERE status IN ('running', 'failed');
