-- Migration 007: Claims, fragments, comparisons, and job queue for thinktank v2

-- ==========================================================================
-- Extend entries table with claim and fragment fields
-- ==========================================================================

-- Claims: fixed-size claim representation (JSON array of {text, confidence})
ALTER TABLE entries ADD COLUMN IF NOT EXISTS claims JSONB NOT NULL DEFAULT '[]'::jsonb;

-- Claims processing status
ALTER TABLE entries ADD COLUMN IF NOT EXISTS claims_status TEXT NOT NULL DEFAULT 'pending'
    CHECK (claims_status IN ('pending', 'distilled', 'verified'));

-- Fragment support: link fragments to their parent artifact
ALTER TABLE entries ADD COLUMN IF NOT EXISTS fragment_of UUID REFERENCES entries(id);
ALTER TABLE entries ADD COLUMN IF NOT EXISTS fragment_index INTEGER;

-- Comparison results: stored as JSON array on the entry
ALTER TABLE entries ADD COLUMN IF NOT EXISTS comparisons JSONB NOT NULL DEFAULT '[]'::jsonb;

-- Precomputed max friction across all comparisons (for fast filtering)
ALTER TABLE entries ADD COLUMN IF NOT EXISTS max_friction DOUBLE PRECISION;

-- Whether this entry needs expensive LLM review
ALTER TABLE entries ADD COLUMN IF NOT EXISTS needs_review BOOLEAN NOT NULL DEFAULT false;

-- Fragment ordering constraint
ALTER TABLE entries ADD CONSTRAINT fragment_index_requires_parent
    CHECK ((fragment_of IS NULL AND fragment_index IS NULL) OR
           (fragment_of IS NOT NULL AND fragment_index IS NOT NULL));

-- ==========================================================================
-- Indexes for new fields
-- ==========================================================================

-- Find all fragments of an artifact
CREATE INDEX IF NOT EXISTS idx_entries_fragment_of
    ON entries(fragment_of)
    WHERE fragment_of IS NOT NULL;

-- Filter by claims_status (for job queue: find entries needing distillation)
CREATE INDEX IF NOT EXISTS idx_entries_claims_status
    ON entries(notebook_id, claims_status);

-- Filter by needs_review (for agents: find entries needing attention)
CREATE INDEX IF NOT EXISTS idx_entries_needs_review
    ON entries(notebook_id, needs_review)
    WHERE needs_review = true;

-- Filter by max_friction (for browsing high-friction entries)
CREATE INDEX IF NOT EXISTS idx_entries_max_friction
    ON entries(notebook_id, max_friction)
    WHERE max_friction IS NOT NULL;

-- ==========================================================================
-- Job queue table
-- ==========================================================================

CREATE TABLE IF NOT EXISTS jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notebook_id UUID NOT NULL REFERENCES notebooks(id),
    job_type TEXT NOT NULL CHECK (job_type IN ('DISTILL_CLAIMS', 'COMPARE_CLAIMS', 'CLASSIFY_TOPIC')),
    status TEXT NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'in_progress', 'completed', 'failed')),
    payload JSONB NOT NULL,
    result JSONB,
    error TEXT,

    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    claimed_at TIMESTAMPTZ,
    claimed_by TEXT,
    completed_at TIMESTAMPTZ,
    timeout_seconds INTEGER NOT NULL DEFAULT 120,
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3
);

COMMENT ON TABLE jobs IS 'Work queue for robot workers (claim distillation, comparison, classification)';

-- Pull next pending job by type (FIFO order)
CREATE INDEX IF NOT EXISTS idx_jobs_pending
    ON jobs(notebook_id, job_type, created)
    WHERE status = 'pending';

-- Find in-progress jobs (for timeout checks)
CREATE INDEX IF NOT EXISTS idx_jobs_in_progress
    ON jobs(claimed_at)
    WHERE status = 'in_progress';

-- Stats per notebook and job type
CREATE INDEX IF NOT EXISTS idx_jobs_notebook_type_status
    ON jobs(notebook_id, job_type, status);
