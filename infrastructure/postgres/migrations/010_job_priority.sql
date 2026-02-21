-- Migration 010: Add priority column to jobs for depth-first pipeline processing
--
-- Higher priority = processed first. Downstream jobs (EMBED, COMPARE) get higher
-- priority so each entry completes its full pipeline before new entries start.

ALTER TABLE jobs ADD COLUMN IF NOT EXISTS priority INTEGER NOT NULL DEFAULT 0;

-- Update the pending job index to include priority for efficient ordering
DROP INDEX IF EXISTS idx_jobs_pending;
CREATE INDEX idx_jobs_pending
    ON jobs(notebook_id, status, priority DESC, created ASC)
    WHERE status = 'pending';
