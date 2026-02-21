-- Migration 004: Coherence links table for semantic similarity
-- Depends on: 002_schema.sql
-- This is a pure relational fallback for coherence links, independent of Apache AGE.
-- When AGE is available, coherence edges also exist in the graph.

-- Coherence links: pairwise semantic similarity between entries
CREATE TABLE IF NOT EXISTS coherence_links (
    entry_id_1 UUID NOT NULL REFERENCES entries(id) ON DELETE CASCADE,
    entry_id_2 UUID NOT NULL REFERENCES entries(id) ON DELETE CASCADE,
    notebook_id UUID NOT NULL REFERENCES notebooks(id) ON DELETE CASCADE,
    similarity DOUBLE PRECISION NOT NULL,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Canonical ordering: entry_id_1 < entry_id_2 to avoid duplicates
    CONSTRAINT coherence_links_ordering CHECK (entry_id_1 < entry_id_2),
    PRIMARY KEY (entry_id_1, entry_id_2)
);

COMMENT ON TABLE coherence_links IS 'Pairwise semantic similarity between entries';
COMMENT ON COLUMN coherence_links.similarity IS 'Cosine similarity score between entry contents (0.0 to 1.0)';

-- Index for finding all coherence links in a notebook
CREATE INDEX IF NOT EXISTS idx_coherence_links_notebook
    ON coherence_links(notebook_id);

-- Index for finding links by similarity score (for threshold queries)
CREATE INDEX IF NOT EXISTS idx_coherence_links_similarity
    ON coherence_links(notebook_id, similarity DESC);

-- Index for finding all links for a specific entry
CREATE INDEX IF NOT EXISTS idx_coherence_links_entry1
    ON coherence_links(entry_id_1);

CREATE INDEX IF NOT EXISTS idx_coherence_links_entry2
    ON coherence_links(entry_id_2);
