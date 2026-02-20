CREATE TABLE IF NOT EXISTS audit_log (
    id          BIGSERIAL PRIMARY KEY,
    ts          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notebook_id UUID REFERENCES notebooks(id) ON DELETE SET NULL,
    author_id   BYTEA,
    action      TEXT NOT NULL,
    target_type TEXT,
    target_id   TEXT,
    detail      JSONB,
    ip_address  INET,
    user_agent  TEXT
);

CREATE INDEX IF NOT EXISTS idx_audit_notebook_ts ON audit_log(notebook_id, ts DESC) WHERE notebook_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_author_ts ON audit_log(author_id, ts DESC) WHERE author_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_audit_action_ts ON audit_log(action, ts DESC);
