-- 012_integration_status.sql
-- Adds integration lifecycle status to entries (probation → integrated/contested)

ALTER TABLE entries ADD COLUMN IF NOT EXISTS integration_status TEXT NOT NULL DEFAULT 'probation'
    CHECK (integration_status IN ('probation', 'integrated', 'contested'));
ALTER TABLE entries ADD COLUMN IF NOT EXISTS expected_comparisons INTEGER;

CREATE INDEX IF NOT EXISTS idx_entries_integration_status
    ON entries(notebook_id, integration_status) WHERE fragment_of IS NULL;

-- Backfill: auto-classify existing fully-processed entries
UPDATE entries SET integration_status = 'integrated'
WHERE claims_status IN ('distilled', 'verified')
  AND embedding IS NOT NULL AND fragment_of IS NULL
  AND (max_friction IS NULL OR max_friction <= 0.2);

UPDATE entries SET integration_status = 'contested'
WHERE claims_status IN ('distilled', 'verified')
  AND embedding IS NOT NULL AND fragment_of IS NULL
  AND max_friction > 0.2;
