-- Confluence crawler implementation: configuration and incremental sync state.
-- Referenced by crawlers.state_ref_id when crawlers.state_provider = 'confluence_state'.

CREATE TABLE IF NOT EXISTS confluence_crawler_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- User-provided configuration (validated against JSON schema)
    -- Schema fields:
    --   base_url (string, required) — Confluence instance URL
    --   username (string, required) — Confluence username/email
    --   api_token (string, required) — Confluence API token
    --   space_key (string, required) — Space key to crawl
    --   include_labels (array, optional) — Only pages with these labels
    --   exclude_labels (array, optional) — Exclude pages with these labels
    --   max_pages (integer, optional) — Max pages per sync (0 = unlimited)
    --   include_attachments (boolean, optional) — Include attachments as entries
    config JSONB NOT NULL,

    -- Incremental sync state (updated after each successful sync)
    -- Schema fields:
    --   space_key (string) — Confluence space key
    --   space_id (integer) — Confluence space ID
    --   last_sync_timestamp (ISO8601) — When last sync completed
    --   pages_synced (integer) — Number of pages fetched in last sync
    --   page_metadata (object) — {page_id: {title, version, last_modified, status, content_hash}}
    sync_state JSONB NOT NULL DEFAULT '{}',

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Index for fast lookup by space key (useful for debugging)
CREATE INDEX idx_confluence_state_by_config_space
    ON confluence_crawler_state
    USING GIN (config jsonb_path_ops);

-- Index for sync tracking
CREATE INDEX idx_confluence_state_sync_timestamp
    ON confluence_crawler_state ((sync_state->>'last_sync_timestamp'));
