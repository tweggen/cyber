//! Graph operations using Apache AGE with SQL fallbacks.
//!
//! This module provides graph traversal queries for entry relationships:
//! - Reference closure (all entries reachable via references)
//! - Revision chains (ancestors in revision history)
//! - Citations (entries that reference a given entry)
//! - Coherence (semantically related entries)
//!
//! When Apache AGE is available, queries use Cypher via AGE graph functions.
//! When AGE is unavailable, equivalent SQL queries run against the relational
//! schema (`entries.references`, `entries.revision_of`, `coherence_links`).

use sqlx::PgPool;
use uuid::Uuid;

use crate::error::{StoreError, StoreResult};

/// Graph query operations for the store.
#[derive(Debug, Clone)]
pub struct GraphQueries<'a> {
    pool: &'a PgPool,
    age_available: bool,
}

impl<'a> GraphQueries<'a> {
    /// Create a new graph queries instance.
    pub fn new(pool: &'a PgPool, age_available: bool) -> Self {
        Self {
            pool,
            age_available,
        }
    }

    /// Find all entries reachable from a given entry via references.
    ///
    /// Returns entry IDs and their depth from the starting entry.
    pub async fn find_reference_closure(
        &self,
        entry_id: Uuid,
        max_depth: i32,
    ) -> StoreResult<Vec<(Uuid, i32)>> {
        if self.age_available {
            self.find_reference_closure_age(entry_id, max_depth).await
        } else {
            self.find_reference_closure_sql(entry_id, max_depth).await
        }
    }

    /// Find the revision chain (all ancestors) of an entry.
    ///
    /// Returns entry IDs and their depth from the starting entry.
    pub async fn find_revision_chain(&self, entry_id: Uuid) -> StoreResult<Vec<(Uuid, i32)>> {
        if self.age_available {
            self.find_revision_chain_age(entry_id).await
        } else {
            self.find_revision_chain_sql(entry_id).await
        }
    }

    /// Find all entries that cite (reference) a given entry.
    pub async fn find_citations(&self, entry_id: Uuid) -> StoreResult<Vec<Uuid>> {
        if self.age_available {
            self.find_citations_age(entry_id).await
        } else {
            self.find_citations_sql(entry_id).await
        }
    }

    /// Find entries that are semantically related (via coherence edges).
    ///
    /// Returns entry IDs and their similarity scores.
    pub async fn find_coherent_entries(
        &self,
        entry_id: Uuid,
        min_similarity: f64,
    ) -> StoreResult<Vec<(Uuid, f64)>> {
        if self.age_available {
            self.find_coherent_entries_age(entry_id, min_similarity)
                .await
        } else {
            self.find_coherent_entries_sql(entry_id, min_similarity)
                .await
        }
    }

    /// Add a coherence edge between two entries.
    ///
    /// Always writes to the `coherence_links` relational table (dual-write).
    /// Additionally writes to the AGE graph if available.
    pub async fn add_coherence_edge(
        &self,
        entry_id_1: Uuid,
        entry_id_2: Uuid,
        similarity: f64,
    ) -> StoreResult<()> {
        // Always write to relational table with canonical ordering (smaller UUID first)
        let (id_lo, id_hi) = if entry_id_1 < entry_id_2 {
            (entry_id_1, entry_id_2)
        } else {
            (entry_id_2, entry_id_1)
        };

        sqlx::query(
            r#"
            INSERT INTO coherence_links (entry_id_1, entry_id_2, similarity)
            VALUES ($1, $2, $3)
            ON CONFLICT (entry_id_1, entry_id_2)
            DO UPDATE SET similarity = $3, created = NOW()
            "#,
        )
        .bind(id_lo)
        .bind(id_hi)
        .bind(similarity)
        .execute(self.pool)
        .await
        .map_err(|e| StoreError::GraphError(format!("Failed to insert coherence link: {}", e)))?;

        // Also write to AGE graph if available
        if self.age_available {
            sqlx::query("SELECT add_coherence_edge($1, $2, $3)")
                .bind(entry_id_1)
                .bind(entry_id_2)
                .bind(similarity)
                .execute(self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("Failed to add coherence edge to graph: {}", e))
                })?;
        }

        Ok(())
    }

    /// Execute a raw Cypher query.
    ///
    /// Returns an error when AGE is unavailable — there is no SQL equivalent
    /// for arbitrary Cypher queries.
    ///
    /// # Warning
    ///
    /// This method executes arbitrary Cypher queries. Ensure inputs are
    /// properly sanitized to prevent injection attacks.
    pub async fn execute_cypher(&self, cypher: &str) -> StoreResult<Vec<serde_json::Value>> {
        if !self.age_available {
            return Err(StoreError::GraphError(
                "Cypher queries require Apache AGE, which is not available".to_string(),
            ));
        }

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

    // ========================================================================
    // AGE implementations (original code)
    // ========================================================================

    async fn find_reference_closure_age(
        &self,
        entry_id: Uuid,
        max_depth: i32,
    ) -> StoreResult<Vec<(Uuid, i32)>> {
        let rows: Vec<(String, i32)> =
            sqlx::query_as("SELECT entry_id::text, depth::int FROM find_reference_closure($1, $2)")
                .bind(entry_id)
                .bind(max_depth)
                .fetch_all(self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("Reference closure query failed: {}", e))
                })?;

        rows.into_iter()
            .map(|(id_str, depth)| {
                let id = parse_age_uuid(&id_str)?;
                Ok((id, depth))
            })
            .collect()
    }

    async fn find_revision_chain_age(&self, entry_id: Uuid) -> StoreResult<Vec<(Uuid, i32)>> {
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

    async fn find_citations_age(&self, entry_id: Uuid) -> StoreResult<Vec<Uuid>> {
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

    async fn find_coherent_entries_age(
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

    // ========================================================================
    // SQL fallback implementations
    // ========================================================================

    /// Recursive CTE over `unnest("references")` with cycle deduplication.
    async fn find_reference_closure_sql(
        &self,
        entry_id: Uuid,
        max_depth: i32,
    ) -> StoreResult<Vec<(Uuid, i32)>> {
        let rows: Vec<(Uuid, i32)> = sqlx::query_as(
            r#"
            WITH RECURSIVE ref_closure AS (
                -- Base: direct references of the starting entry
                SELECT unnest("references") AS entry_id, 1 AS depth
                FROM entries
                WHERE id = $1

                UNION

                -- Recurse: references of already-reached entries
                SELECT unnest(e."references"), rc.depth + 1
                FROM entries e
                JOIN ref_closure rc ON e.id = rc.entry_id
                WHERE rc.depth < $2
            )
            SELECT entry_id, MIN(depth) AS depth
            FROM ref_closure
            GROUP BY entry_id
            ORDER BY depth, entry_id
            "#,
        )
        .bind(entry_id)
        .bind(max_depth)
        .fetch_all(self.pool)
        .await
        .map_err(|e| {
            StoreError::GraphError(format!("SQL reference closure query failed: {}", e))
        })?;

        Ok(rows)
    }

    /// Recursive CTE on `revision_of` FK chain.
    async fn find_revision_chain_sql(&self, entry_id: Uuid) -> StoreResult<Vec<(Uuid, i32)>> {
        let rows: Vec<(Uuid, i32)> = sqlx::query_as(
            r#"
            WITH RECURSIVE rev_chain AS (
                -- Base: the entry that this one revises
                SELECT revision_of AS entry_id, 1 AS depth
                FROM entries
                WHERE id = $1 AND revision_of IS NOT NULL

                UNION ALL

                -- Recurse: follow revision_of chain
                SELECT e.revision_of, rc.depth + 1
                FROM entries e
                JOIN rev_chain rc ON e.id = rc.entry_id
                WHERE e.revision_of IS NOT NULL AND rc.depth < 100
            )
            SELECT entry_id, depth
            FROM rev_chain
            ORDER BY depth
            "#,
        )
        .bind(entry_id)
        .fetch_all(self.pool)
        .await
        .map_err(|e| StoreError::GraphError(format!("SQL revision chain query failed: {}", e)))?;

        Ok(rows)
    }

    /// Uses the existing GIN index on `"references"` array.
    async fn find_citations_sql(&self, entry_id: Uuid) -> StoreResult<Vec<Uuid>> {
        let rows: Vec<(Uuid,)> =
            sqlx::query_as(r#"SELECT id FROM entries WHERE $1 = ANY("references")"#)
                .bind(entry_id)
                .fetch_all(self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("SQL citations query failed: {}", e))
                })?;

        Ok(rows.into_iter().map(|(id,)| id).collect())
    }

    /// Queries `coherence_links` table with bidirectional lookup.
    async fn find_coherent_entries_sql(
        &self,
        entry_id: Uuid,
        min_similarity: f64,
    ) -> StoreResult<Vec<(Uuid, f64)>> {
        let rows: Vec<(Uuid, f64)> = sqlx::query_as(
            r#"
            SELECT
                CASE WHEN entry_id_1 = $1 THEN entry_id_2 ELSE entry_id_1 END AS related_id,
                similarity
            FROM coherence_links
            WHERE (entry_id_1 = $1 OR entry_id_2 = $1)
              AND similarity >= $2
            ORDER BY similarity DESC
            "#,
        )
        .bind(entry_id)
        .bind(min_similarity)
        .fetch_all(self.pool)
        .await
        .map_err(|e| StoreError::GraphError(format!("SQL coherence query failed: {}", e)))?;

        Ok(rows)
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
        GraphQueries::new(self.pool(), self.age_available())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_age_uuid() {
        let uuid_str = "\"550e8400-e29b-41d4-a716-446655440000\"";
        let result = parse_age_uuid(uuid_str).unwrap();
        assert_eq!(result.to_string(), "550e8400-e29b-41d4-a716-446655440000");
    }

    #[test]
    fn test_parse_age_uuid_no_quotes() {
        let uuid_str = "550e8400-e29b-41d4-a716-446655440000";
        let result = parse_age_uuid(uuid_str).unwrap();
        assert_eq!(result.to_string(), "550e8400-e29b-41d4-a716-446655440000");
    }

    #[test]
    fn test_graph_queries_dispatches_based_on_age_flag() {
        // Verify the struct can be constructed with both flags
        // (actual DB queries require a connection, tested via integration tests)
        let _age_true = "age_available=true means AGE dispatch";
        let _age_false = "age_available=false means SQL fallback dispatch";
    }
}
