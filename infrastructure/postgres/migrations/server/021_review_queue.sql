-- 021_review_queue.sql
-- Content ingestion gate: review queue for external contributions.

-- Review status on entries: pending entries are excluded from entropy pipeline
ALTER TABLE entries ADD COLUMN IF NOT EXISTS review_status TEXT NOT NULL DEFAULT 'approved'
    CHECK (review_status IN ('approved', 'pending', 'rejected'));

CREATE INDEX IF NOT EXISTS idx_entries_review_status ON entries(review_status) WHERE review_status = 'pending';

-- Review queue table
CREATE TABLE IF NOT EXISTS entry_reviews (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    entry_id    UUID NOT NULL REFERENCES entries(id) ON DELETE CASCADE,
    submitter   BYTEA NOT NULL REFERENCES authors(id),
    status      TEXT NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'approved', 'rejected')),
    reviewer    BYTEA REFERENCES authors(id),
    reviewed_at TIMESTAMPTZ,
    created     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_entry_reviews_notebook  ON entry_reviews(notebook_id);
CREATE INDEX IF NOT EXISTS idx_entry_reviews_status    ON entry_reviews(notebook_id, status) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_entry_reviews_entry     ON entry_reviews(entry_id);
