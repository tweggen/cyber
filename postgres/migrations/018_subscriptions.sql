-- 018_subscriptions.sql
-- Inter-thinktank subscriptions: higher-classified notebooks subscribe to lower-classified ones.

CREATE TABLE IF NOT EXISTS notebook_subscriptions (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    subscriber_id     UUID NOT NULL REFERENCES notebooks(id),
    source_id         UUID NOT NULL REFERENCES notebooks(id),
    scope             TEXT NOT NULL DEFAULT 'catalog'
                          CHECK (scope IN ('catalog', 'claims', 'entries')),
    topic_filter      TEXT,
    approved_by       BYTEA NOT NULL REFERENCES authors(id),
    sync_watermark    BIGINT NOT NULL DEFAULT 0,
    last_sync_at      TIMESTAMPTZ,
    sync_status       TEXT NOT NULL DEFAULT 'idle'
                          CHECK (sync_status IN ('idle', 'syncing', 'error', 'suspended')),
    sync_error        TEXT,
    mirrored_count    INTEGER NOT NULL DEFAULT 0,
    discount_factor   DOUBLE PRECISION NOT NULL DEFAULT 0.3
                          CHECK (discount_factor > 0 AND discount_factor <= 1.0),
    poll_interval_s   INTEGER NOT NULL DEFAULT 60
                          CHECK (poll_interval_s >= 10),
    embedding_model   TEXT,
    created           TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (subscriber_id, source_id)
);

CREATE INDEX IF NOT EXISTS idx_subscriptions_subscriber ON notebook_subscriptions(subscriber_id);
CREATE INDEX IF NOT EXISTS idx_subscriptions_source     ON notebook_subscriptions(source_id);
