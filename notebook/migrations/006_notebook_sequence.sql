-- Migration 006: Add atomic sequence counter to notebooks
-- Fixes concurrent sequence assignment by using an atomic counter
-- instead of SELECT MAX(sequence) which races between transactions.

ALTER TABLE notebooks ADD COLUMN IF NOT EXISTS current_sequence BIGINT NOT NULL DEFAULT 0;

-- Backfill from existing entries
UPDATE notebooks n
SET current_sequence = COALESCE(
    (SELECT MAX(sequence) FROM entries e WHERE e.notebook_id = n.id),
    0
)
WHERE n.current_sequence = 0;

COMMENT ON COLUMN notebooks.current_sequence IS 'Atomically incremented sequence counter for concurrent writes';
