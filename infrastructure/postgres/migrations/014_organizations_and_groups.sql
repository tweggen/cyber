CREATE TABLE IF NOT EXISTS organizations (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL UNIQUE,
    created     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS groups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name            TEXT NOT NULL,
    created         TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, name)
);

-- DAG edges: parent -> child relationships within an org
CREATE TABLE IF NOT EXISTS group_edges (
    parent_id   UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    child_id    UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    PRIMARY KEY (parent_id, child_id),
    CHECK (parent_id != child_id)
);

-- Principal memberships (many-to-many)
CREATE TABLE IF NOT EXISTS group_memberships (
    author_id   BYTEA NOT NULL REFERENCES authors(id),
    group_id    UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    role        TEXT NOT NULL DEFAULT 'member',
    granted     TIMESTAMPTZ NOT NULL DEFAULT now(),
    granted_by  BYTEA REFERENCES authors(id),
    PRIMARY KEY (author_id, group_id),
    CHECK (role IN ('member', 'admin'))
);

-- Link notebooks to owning groups
ALTER TABLE notebooks ADD COLUMN IF NOT EXISTS owning_group_id UUID REFERENCES groups(id);
