-- Agent registry: trusted AI agents with security labels for job routing.

CREATE TABLE IF NOT EXISTS agents (
    id              TEXT PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    max_level       TEXT NOT NULL DEFAULT 'INTERNAL'
        CHECK (max_level IN ('PUBLIC', 'INTERNAL', 'CONFIDENTIAL', 'SECRET', 'TOP_SECRET')),
    compartments    TEXT[] NOT NULL DEFAULT '{}',
    infrastructure  TEXT,
    registered      TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen       TIMESTAMPTZ
);
