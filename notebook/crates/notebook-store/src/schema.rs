//! Schema definitions and migration utilities.
//!
//! This module provides embedded SQL schema definitions and utilities
//! for managing database migrations.

use sqlx::PgPool;

use crate::error::{StoreError, StoreResult};

/// Embedded migration SQL for the core schema (002_schema.sql).
pub const SCHEMA_MIGRATION: &str = include_str!("../../../migrations/002_schema.sql");

/// Embedded migration SQL for the graph schema (003_graph.sql).
pub const GRAPH_MIGRATION: &str = include_str!("../../../migrations/003_graph.sql");

/// Embedded migration SQL for the coherence links table (004_coherence_links.sql).
pub const COHERENCE_LINKS_MIGRATION: &str =
    include_str!("../../../migrations/004_coherence_links.sql");

/// Embedded migration SQL for users tables (005_users.sql).
pub const USERS_MIGRATION: &str = include_str!("../../../migrations/005_users.sql");

/// Run all pending migrations against the database.
///
/// This function is idempotent - it can be run multiple times safely.
/// Migrations check for existing objects before creating them.
///
/// # Arguments
///
/// * `pool` - Database connection pool
///
/// # Errors
///
/// Returns an error if any migration fails to execute.
pub async fn run_migrations(pool: &PgPool) -> StoreResult<()> {
    tracing::info!("Running database migrations...");

    // Run schema migration
    tracing::debug!("Running schema migration (002_schema.sql)...");
    sqlx::raw_sql(SCHEMA_MIGRATION)
        .execute(pool)
        .await
        .map_err(|e| StoreError::MigrationError(format!("Schema migration failed: {}", e)))?;

    // Run graph migration (requires Apache AGE extension - non-fatal if unavailable)
    tracing::debug!("Running graph migration (003_graph.sql)...");
    match sqlx::raw_sql(GRAPH_MIGRATION).execute(pool).await {
        Ok(_) => tracing::info!("Graph migration completed successfully"),
        Err(e) => tracing::warn!(
            "Graph migration skipped (Apache AGE not available): {}. \
             Graph traversal features will be disabled.",
            e
        ),
    }

    // Run coherence links migration (pure SQL, always succeeds)
    tracing::debug!("Running coherence links migration (004_coherence_links.sql)...");
    sqlx::raw_sql(COHERENCE_LINKS_MIGRATION)
        .execute(pool)
        .await
        .map_err(|e| {
            StoreError::MigrationError(format!("Coherence links migration failed: {}", e))
        })?;

    // Run users migration
    tracing::debug!("Running users migration (005_users.sql)...");
    sqlx::raw_sql(USERS_MIGRATION)
        .execute(pool)
        .await
        .map_err(|e| StoreError::MigrationError(format!("Users migration failed: {}", e)))?;

    tracing::info!("Migrations completed successfully");
    Ok(())
}

/// Check if the schema has been initialized.
///
/// Returns true if the `entries` table exists.
pub async fn is_schema_initialized(pool: &PgPool) -> StoreResult<bool> {
    let result: (bool,) = sqlx::query_as(
        r#"
        SELECT EXISTS (
            SELECT FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name = 'entries'
        )
        "#,
    )
    .fetch_one(pool)
    .await?;

    Ok(result.0)
}

/// Get the current schema version by checking which tables exist.
///
/// Returns:
/// - 0: No tables exist
/// - 1: Only init.sql has run (AGE extension enabled)
/// - 2: Schema tables exist (002_schema.sql)
/// - 3: Graph functions exist (003_graph.sql)
pub async fn get_schema_version(pool: &PgPool) -> StoreResult<u32> {
    // Check if entries table exists (from 002_schema.sql)
    let has_entries: (bool,) = sqlx::query_as(
        r#"
        SELECT EXISTS (
            SELECT FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name = 'entries'
        )
        "#,
    )
    .fetch_one(pool)
    .await?;

    if !has_entries.0 {
        // Check if AGE extension is enabled (from init.sql)
        let has_age: (bool,) = sqlx::query_as(
            r#"
            SELECT EXISTS (
                SELECT FROM pg_extension
                WHERE extname = 'age'
            )
            "#,
        )
        .fetch_one(pool)
        .await?;

        return Ok(if has_age.0 { 1 } else { 0 });
    }

    // Check if graph functions exist (from 003_graph.sql)
    let has_graph_functions: (bool,) = sqlx::query_as(
        r#"
        SELECT EXISTS (
            SELECT FROM pg_proc
            WHERE proname = 'add_entry_vertex'
        )
        "#,
    )
    .fetch_one(pool)
    .await?;

    if !has_graph_functions.0 {
        return Ok(2);
    }

    // Check if users table exists (from 005_users.sql)
    let has_users: (bool,) = sqlx::query_as(
        r#"
        SELECT EXISTS (
            SELECT FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_name = 'users'
        )
        "#,
    )
    .fetch_one(pool)
    .await?;

    Ok(if has_users.0 { 4 } else { 3 })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_schema_migration_embedded() {
        // Verify the migration SQL is properly embedded
        assert!(SCHEMA_MIGRATION.contains("CREATE TABLE IF NOT EXISTS entries"));
        assert!(SCHEMA_MIGRATION.contains("CREATE TABLE IF NOT EXISTS notebooks"));
        assert!(SCHEMA_MIGRATION.contains("CREATE TABLE IF NOT EXISTS authors"));
        assert!(SCHEMA_MIGRATION.contains("CREATE TABLE IF NOT EXISTS notebook_access"));
    }

    #[test]
    fn test_graph_migration_embedded() {
        // Verify the graph migration SQL is properly embedded
        assert!(GRAPH_MIGRATION.contains("create_vlabel"));
        assert!(GRAPH_MIGRATION.contains("create_elabel"));
        assert!(GRAPH_MIGRATION.contains("add_entry_vertex"));
        assert!(GRAPH_MIGRATION.contains("add_reference_edge"));
    }

    #[test]
    fn test_users_migration_embedded() {
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS users"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS user_keys"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS user_quotas"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS usage_log"));
    }

    #[test]
    fn test_coherence_links_migration_embedded() {
        // Verify the coherence links migration SQL is properly embedded
        assert!(COHERENCE_LINKS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS coherence_links"));
        assert!(COHERENCE_LINKS_MIGRATION.contains("entry_id_1"));
        assert!(COHERENCE_LINKS_MIGRATION.contains("entry_id_2"));
        assert!(COHERENCE_LINKS_MIGRATION.contains("similarity"));
        assert!(COHERENCE_LINKS_MIGRATION.contains("coherence_links_ordering"));
    }
}
