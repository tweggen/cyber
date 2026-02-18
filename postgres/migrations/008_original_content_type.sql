-- Migration 008: Track original content type before normalization
ALTER TABLE entries ADD COLUMN IF NOT EXISTS original_content_type TEXT;
