-- 019_mirrored_content.sql
-- Mirrored claims and entries from subscribed source notebooks.

CREATE TABLE IF NOT EXISTS mirrored_claims (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id   UUID NOT NULL REFERENCES notebook_subscriptions(id) ON DELETE CASCADE,
    source_entry_id   UUID NOT NULL,
    notebook_id       UUID NOT NULL REFERENCES notebooks(id),
    claims            JSONB NOT NULL DEFAULT '[]'::jsonb,
    topic             TEXT,
    embedding         DOUBLE PRECISION[],
    source_sequence   BIGINT NOT NULL DEFAULT 0,
    tombstoned        BOOLEAN NOT NULL DEFAULT false,
    mirrored_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscription_id, source_entry_id)
);

CREATE INDEX IF NOT EXISTS idx_mirrored_claims_subscription ON mirrored_claims(subscription_id);
CREATE INDEX IF NOT EXISTS idx_mirrored_claims_notebook     ON mirrored_claims(notebook_id);

CREATE TABLE IF NOT EXISTS mirrored_entries (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscription_id   UUID NOT NULL REFERENCES notebook_subscriptions(id) ON DELETE CASCADE,
    source_entry_id   UUID NOT NULL,
    notebook_id       UUID NOT NULL REFERENCES notebooks(id),
    content           BYTEA NOT NULL,
    content_type      TEXT NOT NULL DEFAULT 'text/plain',
    topic             TEXT,
    source_sequence   BIGINT NOT NULL DEFAULT 0,
    tombstoned        BOOLEAN NOT NULL DEFAULT false,
    mirrored_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscription_id, source_entry_id)
);

CREATE INDEX IF NOT EXISTS idx_mirrored_entries_subscription ON mirrored_entries(subscription_id);
CREATE INDEX IF NOT EXISTS idx_mirrored_entries_notebook     ON mirrored_entries(notebook_id);
