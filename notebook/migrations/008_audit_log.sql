-- Migration 008: Audit log for tracking all notebook operations

CREATE TABLE IF NOT EXISTS audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    actor BYTEA NOT NULL,          -- author_id (32 bytes)
    action TEXT NOT NULL,           -- e.g. 'entry.write', 'access.grant'
    resource TEXT NOT NULL,         -- e.g. 'notebook:{uuid}' or 'entry:{uuid}'
    detail JSONB,                   -- action-specific context
    ip TEXT,
    user_agent TEXT,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_audit_log_created ON audit_log(created);
CREATE INDEX IF NOT EXISTS idx_audit_log_actor ON audit_log(actor, created);
CREATE INDEX IF NOT EXISTS idx_audit_log_resource ON audit_log(resource, created);
CREATE INDEX IF NOT EXISTS idx_audit_log_action ON audit_log(action, created);
