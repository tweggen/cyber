//! Specialized query builders for the storage layer.
//!
//! This module provides type-safe query construction for common
//! query patterns, including:
//!
//! - Batch entry fetching by IDs
//! - Reference graph queries with depth control
//! - Topic-based queries with pagination
//!
//! Owned by: agent-storage

use notebook_core::{AuthorId, EntryId, NotebookId};
use uuid::Uuid;

use crate::Store;
use crate::error::StoreResult;
use crate::models::EntryRow;

/// Query builder for batch entry fetching.
///
/// Efficiently fetches multiple entries by ID in a single query.
#[derive(Debug, Clone)]
pub struct BatchEntryQuery {
    ids: Vec<Uuid>,
}

impl BatchEntryQuery {
    /// Create a new batch query with the given entry IDs.
    pub fn new(ids: impl IntoIterator<Item = EntryId>) -> Self {
        Self {
            ids: ids.into_iter().map(|e| e.0).collect(),
        }
    }

    /// Execute the batch query.
    pub async fn execute(&self, store: &Store) -> StoreResult<Vec<EntryRow>> {
        if self.ids.is_empty() {
            return Ok(Vec::new());
        }

        // Use ANY() for efficient batch lookup
        let rows = sqlx::query_as::<_, EntryRow>(
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE id = ANY($1)
            ORDER BY sequence
            "#,
        )
        .bind(&self.ids)
        .fetch_all(store.pool())
        .await?;

        Ok(rows)
    }

    /// Execute and return entries in the same order as input IDs.
    ///
    /// Missing entries are omitted from the result.
    pub async fn execute_ordered(&self, store: &Store) -> StoreResult<Vec<EntryRow>> {
        let mut rows = self.execute(store).await?;

        // Create lookup map
        let mut id_to_row: std::collections::HashMap<Uuid, EntryRow> =
            rows.drain(..).map(|r| (r.id, r)).collect();

        // Return in input order
        let result: Vec<EntryRow> = self
            .ids
            .iter()
            .filter_map(|id| id_to_row.remove(id))
            .collect();

        Ok(result)
    }
}

/// Query builder for entries by topic with pagination.
#[derive(Debug, Clone)]
pub struct TopicQuery {
    notebook_id: Uuid,
    topic: String,
    after_sequence: Option<i64>,
    limit: Option<i64>,
    newest_first: bool,
}

impl TopicQuery {
    /// Create a new topic query.
    pub fn new(notebook_id: NotebookId, topic: impl Into<String>) -> Self {
        Self {
            notebook_id: notebook_id.0,
            topic: topic.into(),
            after_sequence: None,
            limit: None,
            newest_first: false,
        }
    }

    /// Set cursor for pagination (entries after this sequence).
    pub fn after(mut self, sequence: i64) -> Self {
        self.after_sequence = Some(sequence);
        self
    }

    /// Limit the number of results.
    pub fn limit(mut self, limit: i64) -> Self {
        self.limit = Some(limit);
        self
    }

    /// Order by newest first (descending sequence).
    pub fn newest_first(mut self) -> Self {
        self.newest_first = true;
        self
    }

    /// Execute the query.
    pub async fn execute(&self, store: &Store) -> StoreResult<Vec<EntryRow>> {
        let order = if self.newest_first { "DESC" } else { "ASC" };

        let query = if self.after_sequence.is_some() && self.limit.is_some() {
            format!(
                r#"
                SELECT id, notebook_id, content, content_type, topic,
                       author_id, signature, revision_of, "references",
                       sequence, created, integration_cost
                FROM entries
                WHERE notebook_id = $1 AND topic = $2 AND sequence > $3
                ORDER BY sequence {}
                LIMIT $4
                "#,
                order
            )
        } else if self.after_sequence.is_some() {
            format!(
                r#"
                SELECT id, notebook_id, content, content_type, topic,
                       author_id, signature, revision_of, "references",
                       sequence, created, integration_cost
                FROM entries
                WHERE notebook_id = $1 AND topic = $2 AND sequence > $3
                ORDER BY sequence {}
                "#,
                order
            )
        } else if self.limit.is_some() {
            format!(
                r#"
                SELECT id, notebook_id, content, content_type, topic,
                       author_id, signature, revision_of, "references",
                       sequence, created, integration_cost
                FROM entries
                WHERE notebook_id = $1 AND topic = $2
                ORDER BY sequence {}
                LIMIT $3
                "#,
                order
            )
        } else {
            format!(
                r#"
                SELECT id, notebook_id, content, content_type, topic,
                       author_id, signature, revision_of, "references",
                       sequence, created, integration_cost
                FROM entries
                WHERE notebook_id = $1 AND topic = $2
                ORDER BY sequence {}
                "#,
                order
            )
        };

        let mut q = sqlx::query_as::<_, EntryRow>(&query)
            .bind(self.notebook_id)
            .bind(&self.topic);

        if let Some(after) = self.after_sequence {
            q = q.bind(after);
        }

        if let Some(limit) = self.limit {
            q = q.bind(limit);
        }

        Ok(q.fetch_all(store.pool()).await?)
    }
}

/// Query builder for entries by author with pagination.
#[derive(Debug, Clone)]
pub struct AuthorEntriesQuery {
    notebook_id: Uuid,
    author_id: [u8; 32],
    after_sequence: Option<i64>,
    limit: Option<i64>,
}

impl AuthorEntriesQuery {
    /// Create a new author entries query.
    pub fn new(notebook_id: NotebookId, author_id: AuthorId) -> Self {
        Self {
            notebook_id: notebook_id.0,
            author_id: author_id.0,
            after_sequence: None,
            limit: None,
        }
    }

    /// Set cursor for pagination.
    pub fn after(mut self, sequence: i64) -> Self {
        self.after_sequence = Some(sequence);
        self
    }

    /// Limit results.
    pub fn limit(mut self, limit: i64) -> Self {
        self.limit = Some(limit);
        self
    }

    /// Execute the query.
    pub async fn execute(&self, store: &Store) -> StoreResult<Vec<EntryRow>> {
        let query = if self.after_sequence.is_some() && self.limit.is_some() {
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1 AND author_id = $2 AND sequence > $3
            ORDER BY sequence
            LIMIT $4
            "#
        } else if self.after_sequence.is_some() {
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1 AND author_id = $2 AND sequence > $3
            ORDER BY sequence
            "#
        } else if self.limit.is_some() {
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1 AND author_id = $2
            ORDER BY sequence
            LIMIT $3
            "#
        } else {
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1 AND author_id = $2
            ORDER BY sequence
            "#
        };

        let mut q = sqlx::query_as::<_, EntryRow>(query)
            .bind(self.notebook_id)
            .bind(self.author_id.as_slice());

        if let Some(after) = self.after_sequence {
            q = q.bind(after);
        }

        if let Some(limit) = self.limit {
            q = q.bind(limit);
        }

        Ok(q.fetch_all(store.pool()).await?)
    }
}

/// Query for finding orphan entries (no incoming references).
#[derive(Debug, Clone)]
pub struct OrphanEntriesQuery {
    notebook_id: Uuid,
    limit: Option<i64>,
}

impl OrphanEntriesQuery {
    /// Create a new orphan entries query.
    pub fn new(notebook_id: NotebookId) -> Self {
        Self {
            notebook_id: notebook_id.0,
            limit: None,
        }
    }

    /// Limit results.
    pub fn limit(mut self, limit: i64) -> Self {
        self.limit = Some(limit);
        self
    }

    /// Execute the query.
    ///
    /// Returns entries that are not referenced by any other entry
    /// and are not revisions of other entries.
    pub async fn execute(&self, store: &Store) -> StoreResult<Vec<EntryRow>> {
        let query = if self.limit.is_some() {
            r#"
            SELECT e.id, e.notebook_id, e.content, e.content_type, e.topic,
                   e.author_id, e.signature, e.revision_of, e."references",
                   e.sequence, e.created, e.integration_cost
            FROM entries e
            WHERE e.notebook_id = $1
              AND e.revision_of IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM entries e2
                  WHERE e2.notebook_id = $1 AND e.id = ANY(e2."references")
              )
            ORDER BY e.sequence
            LIMIT $2
            "#
        } else {
            r#"
            SELECT e.id, e.notebook_id, e.content, e.content_type, e.topic,
                   e.author_id, e.signature, e.revision_of, e."references",
                   e.sequence, e.created, e.integration_cost
            FROM entries e
            WHERE e.notebook_id = $1
              AND e.revision_of IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM entries e2
                  WHERE e2.notebook_id = $1 AND e.id = ANY(e2."references")
              )
            ORDER BY e.sequence
            "#
        };

        let mut q = sqlx::query_as::<_, EntryRow>(query).bind(self.notebook_id);

        if let Some(limit) = self.limit {
            q = q.bind(limit);
        }

        Ok(q.fetch_all(store.pool()).await?)
    }
}

/// Query for finding entries with broken references.
#[derive(Debug, Clone)]
pub struct BrokenReferencesQuery {
    notebook_id: Uuid,
}

impl BrokenReferencesQuery {
    /// Create a new broken references query.
    pub fn new(notebook_id: NotebookId) -> Self {
        Self {
            notebook_id: notebook_id.0,
        }
    }

    /// Execute the query.
    ///
    /// Returns entries that reference non-existent entries.
    pub async fn execute(&self, store: &Store) -> StoreResult<Vec<(EntryRow, Vec<Uuid>)>> {
        // Get all entries with references
        let entries: Vec<EntryRow> = sqlx::query_as(
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1 AND cardinality("references") > 0
            ORDER BY sequence
            "#,
        )
        .bind(self.notebook_id)
        .fetch_all(store.pool())
        .await?;

        // Get all entry IDs in this notebook
        let all_ids: Vec<(Uuid,)> =
            sqlx::query_as(r#"SELECT id FROM entries WHERE notebook_id = $1"#)
                .bind(self.notebook_id)
                .fetch_all(store.pool())
                .await?;

        let existing_ids: std::collections::HashSet<Uuid> =
            all_ids.into_iter().map(|(id,)| id).collect();

        // Find entries with broken references
        let mut result = Vec::new();
        for entry in entries {
            let broken: Vec<Uuid> = entry
                .references
                .iter()
                .filter(|r| !existing_ids.contains(r))
                .copied()
                .collect();

            if !broken.is_empty() {
                result.push((entry, broken));
            }
        }

        Ok(result)
    }
}

/// Statistics query for a notebook.
#[derive(Debug, Clone, Default)]
pub struct NotebookStats {
    /// Total number of entries.
    pub total_entries: i64,
    /// Number of unique authors.
    pub unique_authors: i64,
    /// Number of unique topics.
    pub unique_topics: i64,
    /// Average references per entry.
    pub avg_references: f64,
    /// Number of revision chains.
    pub revision_chains: i64,
}

/// Query for notebook statistics.
pub struct NotebookStatsQuery {
    notebook_id: Uuid,
}

impl NotebookStatsQuery {
    /// Create a new stats query.
    pub fn new(notebook_id: NotebookId) -> Self {
        Self {
            notebook_id: notebook_id.0,
        }
    }

    /// Execute the query.
    pub async fn execute(&self, store: &Store) -> StoreResult<NotebookStats> {
        let row: (i64, i64, i64, Option<f64>, i64) = sqlx::query_as(
            r#"
            SELECT
                COUNT(*)::bigint as total_entries,
                COUNT(DISTINCT author_id)::bigint as unique_authors,
                COUNT(DISTINCT topic)::bigint as unique_topics,
                AVG(cardinality("references"))::float8 as avg_references,
                COUNT(DISTINCT revision_of)::bigint as revision_chains
            FROM entries
            WHERE notebook_id = $1
            "#,
        )
        .bind(self.notebook_id)
        .fetch_one(store.pool())
        .await?;

        Ok(NotebookStats {
            total_entries: row.0,
            unique_authors: row.1,
            unique_topics: row.2,
            avg_references: row.3.unwrap_or(0.0),
            revision_chains: row.4,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_batch_query_empty() {
        let query = BatchEntryQuery::new(std::iter::empty::<EntryId>());
        assert!(query.ids.is_empty());
    }

    #[test]
    fn test_topic_query_builder() {
        let query = TopicQuery::new(NotebookId::new(), "test")
            .after(10)
            .limit(50)
            .newest_first();

        assert_eq!(query.after_sequence, Some(10));
        assert_eq!(query.limit, Some(50));
        assert!(query.newest_first);
    }
}
