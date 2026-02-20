-- Organizations & Groups (Hush-2)

CREATE TABLE organizations (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL,
    owner_id    BYTEA NOT NULL REFERENCES authors(id),
    created     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE organization_members (
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    author_id       BYTEA NOT NULL REFERENCES authors(id),
    role            TEXT NOT NULL CHECK (role IN ('owner', 'admin', 'member')),
    joined          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (organization_id, author_id)
);

CREATE TABLE groups (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    created         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX idx_groups_org_name ON groups (organization_id, name);

CREATE TABLE group_members (
    group_id    UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    author_id   BYTEA NOT NULL REFERENCES authors(id),
    joined      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (group_id, author_id)
);

CREATE TABLE group_edges (
    parent_group_id UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    child_group_id  UUID NOT NULL REFERENCES groups(id) ON DELETE CASCADE,
    created         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (parent_group_id, child_group_id),
    CHECK (parent_group_id != child_group_id)
);

CREATE INDEX idx_group_edges_child ON group_edges (child_group_id);

ALTER TABLE notebooks ADD COLUMN owning_group_id UUID REFERENCES groups(id) ON DELETE SET NULL;
