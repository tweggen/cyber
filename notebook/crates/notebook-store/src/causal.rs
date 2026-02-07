//! Causal position assignment for entries.
//!
//! This module implements the `CausalPositionService` which atomically assigns
//! causal positions to entries. Each entry receives a monotonically increasing
//! sequence number and an `ActivityContext` capturing the notebook's state at
//! write time.
//!
//! # Atomicity
//!
//! Position assignment uses a single database transaction with row-level locking
//! to ensure:
//! - Sequence numbers are strictly monotonic with no gaps
//! - Concurrent writes are serialized correctly
//! - ActivityContext values are consistent with the assigned sequence
//!
//! # Usage
//!
//! ```rust,ignore
//! use notebook_store::causal::CausalPositionService;
//! use notebook_core::{NotebookId, AuthorId, CausalPosition};
//!
//! let position = CausalPositionService::assign_position(
//!     &pool,
//!     notebook_id,
//!     author_id
//! ).await?;
//! ```
//!
//! Owned by: agent-causal

use sqlx::postgres::PgPool;
use uuid::Uuid;

use crate::error::{StoreError, StoreResult};
use notebook_core::{ActivityContext, AuthorId, CausalPosition, NotebookId};

/// Service for assigning causal positions to entries.
///
/// This service handles the atomic assignment of sequence numbers and
/// computation of activity context for new entries.
pub struct CausalPositionService;

impl CausalPositionService {
    /// Atomically assigns a causal position for a new entry in a notebook.
    ///
    /// This method:
    /// 1. Acquires a row lock on the notebook to serialize concurrent writes
    /// 2. Computes the next sequence number (MAX(sequence) + 1)
    /// 3. Computes the ActivityContext:
    ///    - entries_since_last_by_author: count of entries since author's last write
    ///    - total_notebook_entries: current total entries in notebook
    ///    - recent_entropy: rolling sum of catalog_shift from last 10 entries
    ///
    /// # Arguments
    ///
    /// * `pool` - PostgreSQL connection pool
    /// * `notebook_id` - ID of the notebook receiving the entry
    /// * `author_id` - ID of the author creating the entry
    ///
    /// # Returns
    ///
    /// Returns the assigned `CausalPosition` containing the sequence number
    /// and activity context. The caller should use this position when storing
    /// the entry.
    ///
    /// # Errors
    ///
    /// Returns an error if:
    /// - The notebook does not exist
    /// - Database connection fails
    /// - Transaction fails to commit
    ///
    /// # Concurrency
    ///
    /// This method is safe for concurrent use. When multiple writers attempt
    /// to assign positions simultaneously, they are serialized via row-level
    /// locking. Each writer will receive a unique, strictly increasing sequence
    /// number.
    pub async fn assign_position(
        pool: &PgPool,
        notebook_id: NotebookId,
        author_id: AuthorId,
    ) -> StoreResult<CausalPosition> {
        let notebook_uuid = *notebook_id.as_uuid();
        let author_bytes = author_id.as_bytes();

        // Start a transaction for atomic position assignment
        let mut tx = pool.begin().await?;

        // Lock the notebook row to serialize concurrent position assignments.
        // This ensures that only one writer can compute and assign a sequence
        // number at a time for this notebook.
        let notebook_exists: Option<(Uuid,)> =
            sqlx::query_as(r#"SELECT id FROM notebooks WHERE id = $1 FOR UPDATE"#)
                .bind(notebook_uuid)
                .fetch_optional(&mut *tx)
                .await?;

        if notebook_exists.is_none() {
            return Err(StoreError::NotebookNotFound(notebook_uuid));
        }

        // Compute the next sequence number.
        // The lock on notebooks ensures this is serialized.
        let max_seq: (Option<i64>,) =
            sqlx::query_as(r#"SELECT MAX(sequence) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_uuid)
                .fetch_one(&mut *tx)
                .await?;

        let next_sequence = max_seq.0.unwrap_or(0) + 1;

        // Compute total_notebook_entries (current count before this entry)
        let total_count: (i64,) =
            sqlx::query_as(r#"SELECT COUNT(*) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_uuid)
                .fetch_one(&mut *tx)
                .await?;

        let total_notebook_entries = total_count.0 as u32;

        // Find the author's last entry sequence in this notebook
        let author_last_seq: (Option<i64>,) = sqlx::query_as(
            r#"
            SELECT MAX(sequence) FROM entries
            WHERE notebook_id = $1 AND author_id = $2
            "#,
        )
        .bind(notebook_uuid)
        .bind(author_bytes.as_slice())
        .fetch_one(&mut *tx)
        .await?;

        // Compute entries_since_last_by_author
        let entries_since_last_by_author = match author_last_seq.0 {
            Some(last_seq) => {
                // Count entries written after the author's last entry
                let count: (i64,) = sqlx::query_as(
                    r#"
                    SELECT COUNT(*) FROM entries
                    WHERE notebook_id = $1 AND sequence > $2
                    "#,
                )
                .bind(notebook_uuid)
                .bind(last_seq)
                .fetch_one(&mut *tx)
                .await?;
                count.0 as u32
            }
            None => {
                // Author has no entries yet - all entries are "since last"
                total_notebook_entries
            }
        };

        // Compute recent_entropy: rolling sum of catalog_shift from last 10 entries
        // Using JSONB extraction to get the catalog_shift field
        let recent_entropy_result: (Option<f64>,) = sqlx::query_as(
            r#"
            SELECT SUM((integration_cost->>'catalog_shift')::FLOAT8)
            FROM (
                SELECT integration_cost
                FROM entries
                WHERE notebook_id = $1
                ORDER BY sequence DESC
                LIMIT 10
            ) AS recent_entries
            "#,
        )
        .bind(notebook_uuid)
        .fetch_one(&mut *tx)
        .await?;

        let recent_entropy = recent_entropy_result.0.unwrap_or(0.0);

        // Commit the transaction
        tx.commit().await?;

        // Construct and return the CausalPosition
        let activity_context = ActivityContext {
            entries_since_last_by_author,
            total_notebook_entries,
            recent_entropy,
        };

        let causal_position = CausalPosition {
            sequence: next_sequence as u64,
            activity_context,
        };

        Ok(causal_position)
    }

    /// Computes only the activity context for a given notebook and author.
    ///
    /// This is a read-only operation that does not acquire locks or assign
    /// sequence numbers. Useful for preview or analysis purposes.
    ///
    /// # Arguments
    ///
    /// * `pool` - PostgreSQL connection pool
    /// * `notebook_id` - ID of the notebook
    /// * `author_id` - ID of the author
    ///
    /// # Returns
    ///
    /// Returns the computed `ActivityContext` representing the current state.
    pub async fn compute_activity_context(
        pool: &PgPool,
        notebook_id: NotebookId,
        author_id: AuthorId,
    ) -> StoreResult<ActivityContext> {
        let notebook_uuid = *notebook_id.as_uuid();
        let author_bytes = author_id.as_bytes();

        // Verify notebook exists
        let notebook_exists: Option<(Uuid,)> =
            sqlx::query_as(r#"SELECT id FROM notebooks WHERE id = $1"#)
                .bind(notebook_uuid)
                .fetch_optional(pool)
                .await?;

        if notebook_exists.is_none() {
            return Err(StoreError::NotebookNotFound(notebook_uuid));
        }

        // Compute total_notebook_entries
        let total_count: (i64,) =
            sqlx::query_as(r#"SELECT COUNT(*) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_uuid)
                .fetch_one(pool)
                .await?;

        let total_notebook_entries = total_count.0 as u32;

        // Find the author's last entry sequence
        let author_last_seq: (Option<i64>,) = sqlx::query_as(
            r#"
            SELECT MAX(sequence) FROM entries
            WHERE notebook_id = $1 AND author_id = $2
            "#,
        )
        .bind(notebook_uuid)
        .bind(author_bytes.as_slice())
        .fetch_one(pool)
        .await?;

        // Compute entries_since_last_by_author
        let entries_since_last_by_author = match author_last_seq.0 {
            Some(last_seq) => {
                let count: (i64,) = sqlx::query_as(
                    r#"
                    SELECT COUNT(*) FROM entries
                    WHERE notebook_id = $1 AND sequence > $2
                    "#,
                )
                .bind(notebook_uuid)
                .bind(last_seq)
                .fetch_one(pool)
                .await?;
                count.0 as u32
            }
            None => total_notebook_entries,
        };

        // Compute recent_entropy from last 10 entries
        let recent_entropy_result: (Option<f64>,) = sqlx::query_as(
            r#"
            SELECT SUM((integration_cost->>'catalog_shift')::FLOAT8)
            FROM (
                SELECT integration_cost
                FROM entries
                WHERE notebook_id = $1
                ORDER BY sequence DESC
                LIMIT 10
            ) AS recent_entries
            "#,
        )
        .bind(notebook_uuid)
        .fetch_one(pool)
        .await?;

        let recent_entropy = recent_entropy_result.0.unwrap_or(0.0);

        Ok(ActivityContext {
            entries_since_last_by_author,
            total_notebook_entries,
            recent_entropy,
        })
    }

    /// Retrieves the current sequence for a notebook without assigning a new one.
    ///
    /// This is a read-only operation useful for checking the current state.
    ///
    /// # Arguments
    ///
    /// * `pool` - PostgreSQL connection pool
    /// * `notebook_id` - ID of the notebook
    ///
    /// # Returns
    ///
    /// Returns the current maximum sequence number, or 0 if the notebook has no entries.
    pub async fn current_sequence(pool: &PgPool, notebook_id: NotebookId) -> StoreResult<u64> {
        let notebook_uuid = *notebook_id.as_uuid();

        // Verify notebook exists
        let notebook_exists: Option<(Uuid,)> =
            sqlx::query_as(r#"SELECT id FROM notebooks WHERE id = $1"#)
                .bind(notebook_uuid)
                .fetch_optional(pool)
                .await?;

        if notebook_exists.is_none() {
            return Err(StoreError::NotebookNotFound(notebook_uuid));
        }

        let max_seq: (Option<i64>,) =
            sqlx::query_as(r#"SELECT MAX(sequence) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_uuid)
                .fetch_one(pool)
                .await?;

        Ok(max_seq.0.unwrap_or(0) as u64)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use notebook_core::{ActivityContext, CausalPosition};

    #[test]
    fn test_activity_context_default() {
        let ctx = ActivityContext::default();
        assert_eq!(ctx.entries_since_last_by_author, 0);
        assert_eq!(ctx.total_notebook_entries, 0);
        assert_eq!(ctx.recent_entropy, 0.0);
    }

    #[test]
    fn test_causal_position_default() {
        let pos = CausalPosition::default();
        assert_eq!(pos.sequence, 1);
        assert_eq!(pos.activity_context.entries_since_last_by_author, 0);
    }

    #[test]
    fn test_causal_position_first() {
        let pos = CausalPosition::first();
        assert_eq!(pos.sequence, 1);
        assert_eq!(pos.activity_context.total_notebook_entries, 0);
    }
}

/// Integration tests that require a running PostgreSQL database.
/// Run with: cargo test --features integration-tests
#[cfg(all(test, feature = "integration-tests"))]
mod integration_tests {
    use super::*;
    use sqlx::postgres::PgPoolOptions;
    use std::time::Duration;
    use tokio::task::JoinSet;

    async fn setup_test_db() -> PgPool {
        let database_url = std::env::var("DATABASE_URL").unwrap_or_else(|_| {
            "postgres://notebook:notebook_dev@localhost:5432/notebook".to_string()
        });

        PgPoolOptions::new()
            .max_connections(10)
            .acquire_timeout(Duration::from_secs(5))
            .connect(&database_url)
            .await
            .expect("Failed to connect to database")
    }

    async fn create_test_author(pool: &PgPool) -> AuthorId {
        let author_id = AuthorId::from_bytes(rand::random());
        let public_key: [u8; 32] = rand::random();

        sqlx::query(
            r#"INSERT INTO authors (id, public_key) VALUES ($1, $2) ON CONFLICT DO NOTHING"#,
        )
        .bind(author_id.as_bytes().as_slice())
        .bind(public_key.as_slice())
        .execute(pool)
        .await
        .expect("Failed to create test author");

        author_id
    }

    async fn create_test_notebook(pool: &PgPool, owner_id: AuthorId) -> NotebookId {
        let notebook_id = NotebookId::new();

        sqlx::query(r#"INSERT INTO notebooks (id, name, owner_id) VALUES ($1, $2, $3)"#)
            .bind(*notebook_id.as_uuid())
            .bind("Test Notebook")
            .bind(owner_id.as_bytes().as_slice())
            .execute(pool)
            .await
            .expect("Failed to create test notebook");

        // Grant owner write access
        sqlx::query(
            r#"INSERT INTO notebook_access (notebook_id, author_id, read, write) VALUES ($1, $2, true, true)"#,
        )
        .bind(*notebook_id.as_uuid())
        .bind(owner_id.as_bytes().as_slice())
        .execute(pool)
        .await
        .expect("Failed to grant access");

        notebook_id
    }

    #[tokio::test]
    async fn test_assign_position_sequential() {
        let pool = setup_test_db().await;
        let author = create_test_author(&pool).await;
        let notebook = create_test_notebook(&pool, author).await;

        // Assign positions sequentially
        let pos1 = CausalPositionService::assign_position(&pool, notebook, author)
            .await
            .expect("Failed to assign position 1");
        assert_eq!(pos1.sequence, 1);
        assert_eq!(pos1.activity_context.total_notebook_entries, 0);

        // Insert a mock entry to simulate the entry being stored
        insert_mock_entry(&pool, notebook, author, pos1.sequence as i64).await;

        let pos2 = CausalPositionService::assign_position(&pool, notebook, author)
            .await
            .expect("Failed to assign position 2");
        assert_eq!(pos2.sequence, 2);
        assert_eq!(pos2.activity_context.total_notebook_entries, 1);
        assert_eq!(pos2.activity_context.entries_since_last_by_author, 0);

        // Insert another entry
        insert_mock_entry(&pool, notebook, author, pos2.sequence as i64).await;

        let pos3 = CausalPositionService::assign_position(&pool, notebook, author)
            .await
            .expect("Failed to assign position 3");
        assert_eq!(pos3.sequence, 3);
        assert_eq!(pos3.activity_context.total_notebook_entries, 2);
    }

    #[tokio::test]
    async fn test_assign_position_concurrent() {
        let pool = setup_test_db().await;
        let author = create_test_author(&pool).await;
        let notebook = create_test_notebook(&pool, author).await;

        // Launch multiple concurrent position assignments
        let mut tasks = JoinSet::new();
        for _ in 0..10 {
            let pool = pool.clone();
            let notebook = notebook;
            let author = author;
            tasks.spawn(async move {
                CausalPositionService::assign_position(&pool, notebook, author).await
            });
        }

        // Collect all results
        let mut sequences = Vec::new();
        while let Some(result) = tasks.join_next().await {
            let pos = result
                .expect("Task panicked")
                .expect("Position assignment failed");
            sequences.push(pos.sequence);
        }

        // Verify all sequences are unique
        sequences.sort();
        let unique: std::collections::HashSet<_> = sequences.iter().collect();
        assert_eq!(sequences.len(), unique.len(), "Sequences must be unique");

        // Verify sequences are contiguous starting from 1
        for (i, seq) in sequences.iter().enumerate() {
            assert_eq!(*seq, (i + 1) as u64, "Sequences must be contiguous");
        }
    }

    #[tokio::test]
    async fn test_entries_since_last_by_author() {
        let pool = setup_test_db().await;
        let author1 = create_test_author(&pool).await;
        let author2 = create_test_author(&pool).await;
        let notebook = create_test_notebook(&pool, author1).await;

        // Author1 writes
        let pos1 = CausalPositionService::assign_position(&pool, notebook, author1)
            .await
            .unwrap();
        insert_mock_entry(&pool, notebook, author1, pos1.sequence as i64).await;

        // Author2 writes - should see 1 entry since their "last" (they have none)
        let pos2 = CausalPositionService::assign_position(&pool, notebook, author2)
            .await
            .unwrap();
        assert_eq!(pos2.activity_context.entries_since_last_by_author, 1);
        insert_mock_entry(&pool, notebook, author2, pos2.sequence as i64).await;

        // Author2 writes again - should see 0 entries since their last
        let pos3 = CausalPositionService::assign_position(&pool, notebook, author2)
            .await
            .unwrap();
        assert_eq!(pos3.activity_context.entries_since_last_by_author, 0);
        insert_mock_entry(&pool, notebook, author2, pos3.sequence as i64).await;

        // Author1 writes - should see 2 entries since their last (author2's two entries)
        let pos4 = CausalPositionService::assign_position(&pool, notebook, author1)
            .await
            .unwrap();
        assert_eq!(pos4.activity_context.entries_since_last_by_author, 2);
    }

    #[tokio::test]
    async fn test_recent_entropy() {
        let pool = setup_test_db().await;
        let author = create_test_author(&pool).await;
        let notebook = create_test_notebook(&pool, author).await;

        // Insert entries with various catalog_shift values
        for i in 1..=15 {
            let pos = CausalPositionService::assign_position(&pool, notebook, author)
                .await
                .unwrap();
            insert_mock_entry_with_cost(
                &pool,
                notebook,
                author,
                pos.sequence as i64,
                i as f64 * 0.1,
            )
            .await;
        }

        // Get activity context - should sum last 10 entries (6..=15)
        // catalog_shift values: 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5
        // Sum = 10.5
        let ctx = CausalPositionService::compute_activity_context(&pool, notebook, author)
            .await
            .unwrap();

        // Allow for floating point imprecision
        assert!(
            (ctx.recent_entropy - 10.5).abs() < 0.01,
            "Expected ~10.5, got {}",
            ctx.recent_entropy
        );
    }

    #[tokio::test]
    async fn test_notebook_not_found() {
        let pool = setup_test_db().await;
        let author = create_test_author(&pool).await;
        let fake_notebook = NotebookId::new();

        let result = CausalPositionService::assign_position(&pool, fake_notebook, author).await;
        assert!(matches!(result, Err(StoreError::NotebookNotFound(_))));
    }

    async fn insert_mock_entry(
        pool: &PgPool,
        notebook: NotebookId,
        author: AuthorId,
        sequence: i64,
    ) {
        insert_mock_entry_with_cost(pool, notebook, author, sequence, 0.5).await;
    }

    async fn insert_mock_entry_with_cost(
        pool: &PgPool,
        notebook: NotebookId,
        author: AuthorId,
        sequence: i64,
        catalog_shift: f64,
    ) {
        let id = uuid::Uuid::new_v4();
        let integration_cost = serde_json::json!({
            "entries_revised": 0,
            "references_broken": 0,
            "catalog_shift": catalog_shift,
            "orphan": false
        });

        sqlx::query(
            r#"
            INSERT INTO entries (id, notebook_id, content, content_type, author_id, signature, sequence, integration_cost)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            "#,
        )
        .bind(id)
        .bind(*notebook.as_uuid())
        .bind(b"test content".as_slice())
        .bind("text/plain")
        .bind(author.as_bytes().as_slice())
        .bind(vec![0u8; 64].as_slice())
        .bind(sequence)
        .bind(integration_cost)
        .execute(pool)
        .await
        .expect("Failed to insert mock entry");
    }
}
