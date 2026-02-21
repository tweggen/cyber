-- Migration 009: Add embedding column for semantic nearest-neighbor comparison

-- Embedding vector stored as plain double precision array (no pgvector dependency)
ALTER TABLE entries ADD COLUMN IF NOT EXISTS embedding double precision[];

-- Extend job_type check to include EMBED_CLAIMS
ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_job_type_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_job_type_check
    CHECK (job_type IN ('DISTILL_CLAIMS', 'COMPARE_CLAIMS', 'CLASSIFY_TOPIC', 'EMBED_CLAIMS'));
