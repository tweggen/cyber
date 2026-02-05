-- Migration 002: Core relational schema for Knowledge Exchange Platform
-- Depends on: init.sql (AGE extension enabled)

-- Authors table: cryptographic identities
-- The author id is a 32-byte BLAKE3 hash of the public key, matching AuthorId in notebook-core.
-- This provides a stable identifier derived from the cryptographic identity.
CREATE TABLE IF NOT EXISTS authors (
    id BYTEA PRIMARY KEY,
    public_key BYTEA NOT NULL,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- AuthorId is 32 bytes (BLAKE3 hash of public key)
    CONSTRAINT author_id_length CHECK (octet_length(id) = 32),
    -- Ed25519 public keys are 32 bytes
    CONSTRAINT public_key_length CHECK (octet_length(public_key) = 32),
    CONSTRAINT unique_public_key UNIQUE (public_key)
);

COMMENT ON TABLE authors IS 'Cryptographic identities for entry authorship';
COMMENT ON COLUMN authors.id IS 'AuthorId - 32-byte BLAKE3 hash of public key';
COMMENT ON COLUMN authors.public_key IS 'Ed25519 public key (32 bytes)';

-- Notebooks table: containers for entries
CREATE TABLE IF NOT EXISTS notebooks (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    owner_id BYTEA NOT NULL REFERENCES authors(id),
    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- owner_id must be a valid AuthorId (32 bytes)
    CONSTRAINT owner_id_length CHECK (octet_length(owner_id) = 32)
);

COMMENT ON TABLE notebooks IS 'Logical containers grouping related entries';

CREATE INDEX IF NOT EXISTS idx_notebooks_owner ON notebooks(owner_id);

-- Notebook access table: permissions for author/notebook pairs
CREATE TABLE IF NOT EXISTS notebook_access (
    notebook_id UUID NOT NULL REFERENCES notebooks(id) ON DELETE CASCADE,
    author_id BYTEA NOT NULL REFERENCES authors(id) ON DELETE CASCADE,
    read BOOLEAN NOT NULL DEFAULT false,
    write BOOLEAN NOT NULL DEFAULT false,
    granted TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- author_id must be a valid AuthorId (32 bytes)
    CONSTRAINT access_author_id_length CHECK (octet_length(author_id) = 32),

    PRIMARY KEY (notebook_id, author_id)
);

COMMENT ON TABLE notebook_access IS 'Access control list for notebooks';

-- Entries table: the core knowledge atoms
CREATE TABLE IF NOT EXISTS entries (
    id UUID PRIMARY KEY,
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    content BYTEA NOT NULL,
    content_type TEXT NOT NULL,
    topic TEXT,
    author_id BYTEA NOT NULL REFERENCES authors(id),
    signature BYTEA NOT NULL,
    revision_of UUID REFERENCES entries(id),
    references UUID[] NOT NULL DEFAULT ARRAY[]::UUID[],
    sequence BIGINT NOT NULL,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    integration_cost JSONB NOT NULL,

    -- author_id must be a valid AuthorId (32 bytes)
    CONSTRAINT entry_author_id_length CHECK (octet_length(author_id) = 32),
    -- Ed25519 signatures are 64 bytes
    CONSTRAINT signature_length CHECK (octet_length(signature) = 64),
    -- Sequence must be positive and unique per notebook
    CONSTRAINT positive_sequence CHECK (sequence > 0),
    CONSTRAINT unique_notebook_sequence UNIQUE (notebook_id, sequence)
);

COMMENT ON TABLE entries IS 'Knowledge atoms - the fundamental units of the platform';
COMMENT ON COLUMN entries.content IS 'Entry payload as bytes (text stored as UTF-8)';
COMMENT ON COLUMN entries.content_type IS 'MIME type indicating content interpretation';
COMMENT ON COLUMN entries.topic IS 'Optional grouping label for catalog organization';
COMMENT ON COLUMN entries.signature IS 'Ed25519 signature over entry content (64 bytes)';
COMMENT ON COLUMN entries.revision_of IS 'Link to entry this revises (forms revision chain)';
COMMENT ON COLUMN entries.references IS 'Array of entry IDs this entry references';
COMMENT ON COLUMN entries.sequence IS 'Monotonically increasing position within notebook';
COMMENT ON COLUMN entries.integration_cost IS 'Entropy metrics computed at write time';

-- Primary query pattern: entries in sequence order
CREATE INDEX IF NOT EXISTS idx_entries_notebook_sequence
    ON entries(notebook_id, sequence);

-- Topic filtering for BROWSE operation
CREATE INDEX IF NOT EXISTS idx_entries_notebook_topic
    ON entries(notebook_id, topic)
    WHERE topic IS NOT NULL;

-- Revision chain traversal
CREATE INDEX IF NOT EXISTS idx_entries_revision_of
    ON entries(revision_of)
    WHERE revision_of IS NOT NULL;

-- Reference array containment queries (find entries referencing X)
CREATE INDEX IF NOT EXISTS idx_entries_references
    ON entries USING GIN (references);

-- Author's entries (for activity context computation)
CREATE INDEX IF NOT EXISTS idx_entries_notebook_author
    ON entries(notebook_id, author_id);

-- Enable pg_trgm for text search if not already enabled
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Full-text search index on content
-- Note: This indexes the bytea as text which works for text/* content types
-- Binary content will not match text queries (which is correct behavior)
CREATE INDEX IF NOT EXISTS idx_entries_content_search
    ON entries USING GIN (encode(content, 'escape') gin_trgm_ops);
