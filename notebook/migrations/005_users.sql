-- Migration 005: User management tables for authentication and quotas
-- Depends on: 002_schema.sql (authors table)

-- Users: web identity wrapping cryptographic authors
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username TEXT UNIQUE NOT NULL,
    display_name TEXT,
    password_hash TEXT NOT NULL,
    author_id BYTEA NOT NULL REFERENCES authors(id),
    role TEXT NOT NULL DEFAULT 'user',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Server-managed signing keys for web users
CREATE TABLE IF NOT EXISTS user_keys (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    encrypted_private_key BYTEA NOT NULL
);

-- Quotas
CREATE TABLE IF NOT EXISTS user_quotas (
    user_id UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    max_notebooks INTEGER NOT NULL DEFAULT 10,
    max_entries_per_notebook INTEGER NOT NULL DEFAULT 1000,
    max_entry_size_bytes INTEGER NOT NULL DEFAULT 1048576,
    max_total_storage_bytes BIGINT NOT NULL DEFAULT 104857600
);

-- Usage log (append-only)
CREATE TABLE IF NOT EXISTS usage_log (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    author_id BYTEA NOT NULL,
    action TEXT NOT NULL,
    resource_type TEXT,
    resource_id TEXT,
    details JSONB,
    ip_address TEXT,
    created TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
CREATE INDEX IF NOT EXISTS idx_users_author_id ON users(author_id);
CREATE INDEX IF NOT EXISTS idx_usage_log_user_id ON usage_log(user_id);
CREATE INDEX IF NOT EXISTS idx_usage_log_created ON usage_log(created);
CREATE INDEX IF NOT EXISTS idx_usage_log_action ON usage_log(action);
CREATE INDEX IF NOT EXISTS idx_usage_log_resource ON usage_log(resource_type, resource_id);
