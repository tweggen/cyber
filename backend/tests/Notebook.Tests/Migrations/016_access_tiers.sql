-- Access tiers: replace boolean read/write columns with a single tier column.
-- Tiers: existence (can see notebook exists), read, read_write, admin.

ALTER TABLE notebook_access ADD COLUMN tier TEXT NOT NULL DEFAULT 'read_write'
    CHECK (tier IN ('existence', 'read', 'read_write', 'admin'));

-- Backfill from existing booleans
UPDATE notebook_access SET tier = CASE
    WHEN read AND write THEN 'read_write'
    WHEN read AND NOT write THEN 'read'
    ELSE 'existence'
END;

ALTER TABLE notebook_access DROP COLUMN read;
ALTER TABLE notebook_access DROP COLUMN write;
