//! Graph operations using Apache AGE.
//!
//! This module provides graph traversal queries for entry relationships:
//! - Reference closure (all entries reachable via references)
//! - Revision chains (ancestors in revision history)
//! - Citations (entries that reference a given entry)
//! - Coherence (semantically related entries)

use sqlx::PgPool;
use uuid::Uuid;

use crate::error::{StoreError, StoreResult};

/// Graph query operations for the store.
#[derive(Debug, Clone)]
pub struct GraphQueries<'a> {
    pool: &'a PgPool,
}

impl<'a> GraphQueries<'a> {
    /// Create a new graph queries instance.
    pub fn new(pool: &'a PgPool) -> Self {
        Self { pool }
    }

    /// Find all entries reachable from a given entry via references.
    ///
    /// Returns entry IDs and their depth from the starting entry.
    pub async fn find_reference_closure(
        &self,
        entry_id: Uuid,
        max_depth: i32,
    ) -> StoreResult<Vec<(Uuid, i32)>> {
        let rows: Vec<(String, i32)> = sqlx::query_as(
            "SELECT entry_id::text, depth::int FROM find_reference_closure($1, $2)",
        )
        .bind(entry_id)
        .bind(max_depth)
        .fetch_all(self.pool)
        .await
        .map_err(|e| StoreError::GraphError(format!("Reference closure query failed: {}", e)))?;

        // Parse UUIDs from AGE string format
        rows.into_iter()
            .map(|(id_str, depth)| {
                let id = parse_age_uuid(&id_str)?;
                Ok((id, depth))
            })
            .collect()
    }

    /// Find the revision chain (all ancestors) of an entry.
    ///
    /// Returns entry IDs and their depth from the starting entry.
    pub async fn find_revision_chain(&self, entry_id: Uuid) -> StoreResult<Vec<(Uuid, i32)>> {
        let rows: Vec<(String, i32)> =
            sqlx::query_as("SELECT entry_id::text, depth::int FROM find_revision_chain($1)")
                .bind(entry_id)
                .fetch_all(self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("Revision chain query failed: {}", e))
                })?;

        rows.into_iter()
            .map(|(id_str, depth)| {
                let id = parse_age_uuid(&id_str)?;
                Ok((id, depth))
            })
            .collect()
    }

    /// Find all entries that cite (reference) a given entry.
    pub async fn find_citations(&self, entry_id: Uuid) -> StoreResult<Vec<Uuid>> {
        let rows: Vec<(String,)> =
            sqlx::query_as("SELECT citing_entry_id::text FROM find_citations($1)")
                .bind(entry_id)
                .fetch_all(self.pool)
                .await
                .map_err(|e| StoreError::GraphError(format!("Citations query failed: {}", e)))?;

        rows.into_iter()
            .map(|(id_str,)| parse_age_uuid(&id_str))
            .collect()
    }

    /// Find entries that are semantically related (via coherence edges).
    ///
    /// Returns entry IDs and their similarity scores.
    pub async fn find_coherent_entries(
        &self,
        entry_id: Uuid,
        min_similarity: f64,
    ) -> StoreResult<Vec<(Uuid, f64)>> {
        let rows: Vec<(String, f64)> = sqlx::query_as(
            "SELECT related_entry_id::text, similarity::float8 FROM find_coherent_entries($1, $2)",
        )
        .bind(entry_id)
        .bind(min_similarity)
        .fetch_all(self.pool)
        .await
        .map_err(|e| StoreError::GraphError(format!("Coherence query failed: {}", e)))?;

        rows.into_iter()
            .map(|(id_str, similarity)| {
                let id = parse_age_uuid(&id_str)?;
                Ok((id, similarity))
            })
            .collect()
    }

    /// Add a coherence edge between two entries.
    ///
    /// This is typically called by the entropy service when it detects
    /// semantic similarity between entries.
    pub async fn add_coherence_edge(
        &self,
        entry_id_1: Uuid,
        entry_id_2: Uuid,
        similarity: f64,
    ) -> StoreResult<()> {
        sqlx::query("SELECT add_coherence_edge($1, $2, $3)")
            .bind(entry_id_1)
            .bind(entry_id_2)
            .bind(similarity)
            .execute(self.pool)
            .await
            .map_err(|e| StoreError::GraphError(format!("Failed to add coherence edge: {}", e)))?;

        Ok(())
    }

    /// Execute a raw Cypher query.
    ///
    /// Use this for custom graph queries not covered by the helper methods.
    /// The query should be a valid Cypher query without the cypher() wrapper.
    ///
    /// # Warning
    ///
    /// This method executes arbitrary Cypher queries. Ensure inputs are
    /// properly sanitized to prevent injection attacks.
    pub async fn execute_cypher(&self, cypher: &str) -> StoreResult<Vec<serde_json::Value>> {
        let query = format!(
            r#"
            SELECT * FROM cypher('notebook_graph', $$
                {}
            $$) AS (result agtype)
            "#,
            cypher
        );

        let rows: Vec<(serde_json::Value,)> = sqlx::query_as(&query)
            .fetch_all(self.pool)
            .await
            .map_err(|e| StoreError::GraphError(format!("Cypher query failed: {}", e)))?;

        Ok(rows.into_iter().map(|(v,)| v).collect())
    }
}

/// Parse a UUID from AGE's string format.
///
/// AGE returns strings with quotes, e.g., `"550e8400-e29b-41d4-a716-446655440000"`
fn parse_age_uuid(s: &str) -> StoreResult<Uuid> {
    // Remove surrounding quotes if present
    let s = s.trim_matches('"');
    Uuid::parse_str(s).map_err(|e| StoreError::GraphError(format!("Invalid UUID from AGE: {}", e)))
}

/// Extension trait to add graph queries to the Store.
pub trait GraphQueryExt {
    /// Get graph query operations.
    fn graph(&self) -> GraphQueries<'_>;
}

impl crate::Store {
    /// Get graph query operations for this store.
    pub fn graph(&self) -> GraphQueries<'_> {
        GraphQueries::new(self.pool())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_age_uuid() {
        let uuid_str = "\"550e8400-e29b-41d4-a716-446655440000\"";
        let result = parse_age_uuid(uuid_str).unwrap();
        assert_eq!(
            result.to_string(),
            "550e8400-e29b-41d4-a716-446655440000"
        );
    }

    #[test]
    fn test_parse_age_uuid_no_quotes() {
        let uuid_str = "550e8400-e29b-41d4-a716-446655440000";
        let result = parse_age_uuid(uuid_str).unwrap();
        assert_eq!(
            result.to_string(),
            "550e8400-e29b-41d4-a716-446655440000"
        );
    }
}
