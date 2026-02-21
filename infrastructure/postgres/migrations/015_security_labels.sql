-- Security labels: classification levels and compartments on notebooks,
-- clearance records for principals (per-organization).
-- Uses TEXT + CHECK instead of PostgreSQL ENUM for EF Core compatibility.

ALTER TABLE notebooks ADD COLUMN IF NOT EXISTS classification TEXT NOT NULL DEFAULT 'INTERNAL'
    CHECK (classification IN ('PUBLIC', 'INTERNAL', 'CONFIDENTIAL', 'SECRET', 'TOP_SECRET'));
ALTER TABLE notebooks ADD COLUMN IF NOT EXISTS compartments TEXT[] NOT NULL DEFAULT '{}';

CREATE TABLE IF NOT EXISTS principal_clearances (
    author_id       BYTEA NOT NULL REFERENCES authors(id),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    max_level       TEXT NOT NULL DEFAULT 'INTERNAL'
        CHECK (max_level IN ('PUBLIC', 'INTERNAL', 'CONFIDENTIAL', 'SECRET', 'TOP_SECRET')),
    compartments    TEXT[] NOT NULL DEFAULT '{}',
    granted         TIMESTAMPTZ NOT NULL DEFAULT now(),
    granted_by      BYTEA REFERENCES authors(id),
    PRIMARY KEY (author_id, organization_id)
);
