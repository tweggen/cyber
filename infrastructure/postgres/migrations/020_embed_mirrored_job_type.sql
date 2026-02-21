-- 020_embed_mirrored_job_type.sql
-- Extend jobs CHECK constraint to allow EMBED_MIRRORED job type.

ALTER TABLE jobs DROP CONSTRAINT IF EXISTS jobs_job_type_check;
ALTER TABLE jobs ADD CONSTRAINT jobs_job_type_check
    CHECK (job_type IN ('DISTILL_CLAIMS','COMPARE_CLAIMS','CLASSIFY_TOPIC','EMBED_CLAIMS','EMBED_MIRRORED'));
