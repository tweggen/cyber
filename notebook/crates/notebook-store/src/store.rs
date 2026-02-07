//! Main store implementation for database operations.
//!
//! The `Store` type provides all CRUD operations for entries,
//! notebooks, authors, and access control.

use sqlx::postgres::{PgPool, PgPoolOptions};
use uuid::Uuid;

use crate::error::{StoreError, StoreResult};
use crate::models::*;
use crate::schema;

/// Configuration for connecting to the database.
#[derive(Debug, Clone)]
pub struct StoreConfig {
    /// Database connection URL.
    pub database_url: String,
    /// Maximum number of connections in the pool.
    pub max_connections: u32,
    /// Minimum number of connections to maintain.
    pub min_connections: u32,
    /// Run migrations on connect.
    pub run_migrations: bool,
}

impl Default for StoreConfig {
    fn default() -> Self {
        Self {
            database_url: "postgres://notebook:notebook_dev@localhost:5432/notebook".to_string(),
            max_connections: 10,
            min_connections: 1,
            run_migrations: true,
        }
    }
}

impl StoreConfig {
    /// Create configuration from environment variables.
    ///
    /// Reads:
    /// - `DATABASE_URL` - Required database connection string
    /// - `DATABASE_MAX_CONNECTIONS` - Optional, defaults to 10
    /// - `DATABASE_MIN_CONNECTIONS` - Optional, defaults to 1
    /// - `DATABASE_RUN_MIGRATIONS` - Optional, defaults to true
    pub fn from_env() -> StoreResult<Self> {
        let database_url = std::env::var("DATABASE_URL").map_err(|_| {
            StoreError::ConfigError("DATABASE_URL environment variable not set".to_string())
        })?;

        let max_connections = std::env::var("DATABASE_MAX_CONNECTIONS")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(10);

        let min_connections = std::env::var("DATABASE_MIN_CONNECTIONS")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(1);

        let run_migrations = std::env::var("DATABASE_RUN_MIGRATIONS")
            .ok()
            .map(|s| s.to_lowercase() != "false" && s != "0")
            .unwrap_or(true);

        Ok(Self {
            database_url,
            max_connections,
            min_connections,
            run_migrations,
        })
    }
}

/// Database store for the Knowledge Exchange Platform.
///
/// Provides type-safe operations for all database tables.
#[derive(Debug, Clone)]
pub struct Store {
    pool: PgPool,
    /// Whether Apache AGE graph extension is available.
    age_available: bool,
}

impl Store {
    /// Connect to the database with the given configuration.
    ///
    /// Optionally runs migrations if `config.run_migrations` is true.
    pub async fn connect(config: StoreConfig) -> StoreResult<Self> {
        tracing::info!("Connecting to database...");

        let pool = PgPoolOptions::new()
            .max_connections(config.max_connections)
            .min_connections(config.min_connections)
            .connect(&config.database_url)
            .await?;

        tracing::info!("Connected to database");

        if config.run_migrations {
            schema::run_migrations(&pool).await?;
        }

        // Detect AGE availability: schema version >= 3 means graph functions exist
        let age_available = match schema::get_schema_version(&pool).await {
            Ok(version) => {
                let available = version >= 3;
                if available {
                    tracing::info!("Apache AGE detected — graph queries enabled");
                } else {
                    tracing::info!(
                        "Apache AGE not available (schema version {}) — using SQL fallbacks",
                        version
                    );
                }
                available
            }
            Err(e) => {
                tracing::warn!(
                    "Failed to detect schema version: {} — using SQL fallbacks",
                    e
                );
                false
            }
        };

        Ok(Self {
            pool,
            age_available,
        })
    }

    /// Create a store from an existing connection pool.
    ///
    /// Defaults to `age_available: false` since we cannot detect without querying.
    pub fn from_pool(pool: PgPool) -> Self {
        Self {
            pool,
            age_available: false,
        }
    }

    /// Whether Apache AGE graph extension is available.
    pub fn age_available(&self) -> bool {
        self.age_available
    }

    /// Get a reference to the connection pool.
    pub fn pool(&self) -> &PgPool {
        &self.pool
    }

    // ==================== Author Operations ====================

    /// Insert a new author.
    pub async fn insert_author(&self, author: &NewAuthor) -> StoreResult<AuthorRow> {
        let row = sqlx::query_as::<_, AuthorRow>(
            r#"
            INSERT INTO authors (id, public_key)
            VALUES ($1, $2)
            RETURNING id, public_key, created
            "#,
        )
        .bind(author.id.as_slice())
        .bind(author.public_key.as_slice())
        .fetch_one(&self.pool)
        .await?;

        Ok(row)
    }

    /// Get an author by ID (32-byte AuthorId).
    pub async fn get_author(&self, id: &[u8; 32]) -> StoreResult<AuthorRow> {
        sqlx::query_as::<_, AuthorRow>(
            r#"SELECT id, public_key, created FROM authors WHERE id = $1"#,
        )
        .bind(id.as_slice())
        .fetch_optional(&self.pool)
        .await?
        .ok_or_else(|| {
            // Format the ID for the error message
            let id_hex: String = id.iter().map(|b| format!("{:02x}", b)).collect();
            StoreError::ConfigError(format!("Author not found: {}", id_hex))
        })
    }

    /// Get an author by public key.
    pub async fn get_author_by_public_key(
        &self,
        public_key: &[u8],
    ) -> StoreResult<Option<AuthorRow>> {
        Ok(sqlx::query_as::<_, AuthorRow>(
            r#"SELECT id, public_key, created FROM authors WHERE public_key = $1"#,
        )
        .bind(public_key)
        .fetch_optional(&self.pool)
        .await?)
    }

    /// Check if an author exists.
    pub async fn author_exists(&self, id: &[u8; 32]) -> StoreResult<bool> {
        let result: (bool,) =
            sqlx::query_as(r#"SELECT EXISTS (SELECT 1 FROM authors WHERE id = $1)"#)
                .bind(id.as_slice())
                .fetch_one(&self.pool)
                .await?;

        Ok(result.0)
    }

    // ==================== Notebook Operations ====================

    /// Insert a new notebook.
    pub async fn insert_notebook(&self, notebook: &NewNotebook) -> StoreResult<NotebookRow> {
        // Verify owner exists
        if !self.author_exists(&notebook.owner_id).await? {
            let id_hex: String = notebook
                .owner_id
                .iter()
                .map(|b| format!("{:02x}", b))
                .collect();
            return Err(StoreError::ConfigError(format!(
                "Owner author not found: {}",
                id_hex
            )));
        }

        let row = sqlx::query_as::<_, NotebookRow>(
            r#"
            INSERT INTO notebooks (id, name, owner_id)
            VALUES ($1, $2, $3)
            RETURNING id, name, owner_id, created
            "#,
        )
        .bind(notebook.id)
        .bind(&notebook.name)
        .bind(notebook.owner_id.as_slice())
        .fetch_one(&self.pool)
        .await?;

        // Grant owner full access
        self.grant_access(&NewNotebookAccess {
            notebook_id: notebook.id,
            author_id: notebook.owner_id,
            read: true,
            write: true,
        })
        .await?;

        Ok(row)
    }

    /// Get a notebook by ID.
    pub async fn get_notebook(&self, id: Uuid) -> StoreResult<NotebookRow> {
        sqlx::query_as::<_, NotebookRow>(
            r#"SELECT id, name, owner_id, created FROM notebooks WHERE id = $1"#,
        )
        .bind(id)
        .fetch_optional(&self.pool)
        .await?
        .ok_or(StoreError::NotebookNotFound(id))
    }

    /// List all notebooks for an author (owned or with access).
    pub async fn list_notebooks_for_author(
        &self,
        author_id: &[u8; 32],
    ) -> StoreResult<Vec<NotebookRow>> {
        Ok(sqlx::query_as::<_, NotebookRow>(
            r#"
            SELECT DISTINCT n.id, n.name, n.owner_id, n.created
            FROM notebooks n
            LEFT JOIN notebook_access a ON n.id = a.notebook_id
            WHERE n.owner_id = $1 OR a.author_id = $1
            ORDER BY n.created DESC
            "#,
        )
        .bind(author_id.as_slice())
        .fetch_all(&self.pool)
        .await?)
    }

    // ==================== Access Control Operations ====================

    /// Grant access to a notebook.
    pub async fn grant_access(&self, access: &NewNotebookAccess) -> StoreResult<NotebookAccessRow> {
        let row = sqlx::query_as::<_, NotebookAccessRow>(
            r#"
            INSERT INTO notebook_access (notebook_id, author_id, read, write)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (notebook_id, author_id)
            DO UPDATE SET read = $3, write = $4, granted = NOW()
            RETURNING notebook_id, author_id, read, write, granted
            "#,
        )
        .bind(access.notebook_id)
        .bind(access.author_id.as_slice())
        .bind(access.read)
        .bind(access.write)
        .fetch_one(&self.pool)
        .await?;

        Ok(row)
    }

    /// Check if an author has read access to a notebook.
    pub async fn has_read_access(
        &self,
        notebook_id: Uuid,
        author_id: &[u8; 32],
    ) -> StoreResult<bool> {
        let result: (bool,) = sqlx::query_as(
            r#"
            SELECT EXISTS (
                SELECT 1 FROM notebook_access
                WHERE notebook_id = $1 AND author_id = $2 AND read = true
            )
            "#,
        )
        .bind(notebook_id)
        .bind(author_id.as_slice())
        .fetch_one(&self.pool)
        .await?;

        Ok(result.0)
    }

    /// Check if an author has write access to a notebook.
    pub async fn has_write_access(
        &self,
        notebook_id: Uuid,
        author_id: &[u8; 32],
    ) -> StoreResult<bool> {
        let result: (bool,) = sqlx::query_as(
            r#"
            SELECT EXISTS (
                SELECT 1 FROM notebook_access
                WHERE notebook_id = $1 AND author_id = $2 AND write = true
            )
            "#,
        )
        .bind(notebook_id)
        .bind(author_id.as_slice())
        .fetch_one(&self.pool)
        .await?;

        Ok(result.0)
    }

    /// List all access grants for a notebook.
    pub async fn list_notebook_access(
        &self,
        notebook_id: Uuid,
    ) -> StoreResult<Vec<NotebookAccessRow>> {
        Ok(sqlx::query_as::<_, NotebookAccessRow>(
            r#"
            SELECT notebook_id, author_id, read, write, granted
            FROM notebook_access
            WHERE notebook_id = $1
            ORDER BY granted
            "#,
        )
        .bind(notebook_id)
        .fetch_all(&self.pool)
        .await?)
    }

    // ==================== Entry Operations ====================

    /// Get the next sequence number for a notebook.
    async fn next_sequence(&self, notebook_id: Uuid) -> StoreResult<i64> {
        let result: (Option<i64>,) =
            sqlx::query_as(r#"SELECT MAX(sequence) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_id)
                .fetch_one(&self.pool)
                .await?;

        Ok(result.0.unwrap_or(0) + 1)
    }

    /// Insert a new entry.
    ///
    /// This method:
    /// 1. Validates signature length
    /// 2. Verifies notebook exists
    /// 3. Validates all references exist
    /// 4. Validates revision_of entry exists (if specified)
    /// 5. Assigns the next sequence number
    /// 6. Inserts the entry
    /// 7. Creates graph vertex and edges
    pub async fn insert_entry(&self, entry: &NewEntry) -> StoreResult<EntryRow> {
        if entry.signature.len() != 64 {
            return Err(StoreError::InvalidSignatureLength(entry.signature.len()));
        }

        // Verify notebook exists
        let _ = self.get_notebook(entry.notebook_id).await?;

        // Validate references
        for ref_id in &entry.references {
            if !self.entry_exists(*ref_id).await? {
                return Err(StoreError::InvalidReference(*ref_id));
            }
        }

        // Validate revision_of
        if let Some(revision_of) = entry.revision_of
            && !self.entry_exists(revision_of).await?
        {
            return Err(StoreError::InvalidRevision(revision_of));
        }

        // Get next sequence number
        let sequence = self.next_sequence(entry.notebook_id).await?;

        // Serialize integration cost
        let integration_cost_json = serde_json::to_value(&entry.integration_cost)?;

        // Insert entry
        let row = sqlx::query_as::<_, EntryRow>(
            r#"
            INSERT INTO entries (
                id, notebook_id, content, content_type, topic,
                author_id, signature, revision_of, "references",
                sequence, integration_cost
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            RETURNING id, notebook_id, content, content_type, topic,
                      author_id, signature, revision_of, "references",
                      sequence, created, integration_cost
            "#,
        )
        .bind(entry.id)
        .bind(entry.notebook_id)
        .bind(&entry.content)
        .bind(&entry.content_type)
        .bind(&entry.topic)
        .bind(entry.author_id.as_slice())
        .bind(&entry.signature)
        .bind(entry.revision_of)
        .bind(&entry.references)
        .bind(sequence)
        .bind(integration_cost_json)
        .fetch_one(&self.pool)
        .await?;

        // Add graph vertex (only if AGE is available; best effort)
        if self.age_available
            && let Err(e) = self.add_entry_to_graph(&row).await
        {
            tracing::warn!("Failed to add entry to graph: {}", e);
        }

        Ok(row)
    }

    /// Check if an entry exists.
    pub async fn entry_exists(&self, id: Uuid) -> StoreResult<bool> {
        let result: (bool,) =
            sqlx::query_as(r#"SELECT EXISTS (SELECT 1 FROM entries WHERE id = $1)"#)
                .bind(id)
                .fetch_one(&self.pool)
                .await?;

        Ok(result.0)
    }

    /// Get an entry by ID.
    pub async fn get_entry(&self, id: Uuid) -> StoreResult<EntryRow> {
        sqlx::query_as::<_, EntryRow>(
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE id = $1
            "#,
        )
        .bind(id)
        .fetch_optional(&self.pool)
        .await?
        .ok_or(StoreError::EntryNotFound(id))
    }

    /// Query entries with filters.
    pub async fn query_entries(&self, query: &EntryQuery) -> StoreResult<Vec<EntryRow>> {
        let notebook_id = query.notebook_id.ok_or_else(|| {
            StoreError::ConfigError("notebook_id is required for entry queries".to_string())
        })?;

        // Build dynamic query
        let mut sql = String::from(
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE notebook_id = $1
            "#,
        );

        let mut param_idx = 2;

        if query.topic.is_some() {
            sql.push_str(&format!(" AND topic = ${}", param_idx));
            param_idx += 1;
        }

        if query.author_id.is_some() {
            sql.push_str(&format!(" AND author_id = ${}", param_idx));
            param_idx += 1;
        }

        if query.after_sequence.is_some() {
            sql.push_str(&format!(" AND sequence > ${}", param_idx));
            param_idx += 1;
        }

        if query.newest_first {
            sql.push_str(" ORDER BY sequence DESC");
        } else {
            sql.push_str(" ORDER BY sequence ASC");
        }

        if query.limit.is_some() {
            sql.push_str(&format!(" LIMIT ${}", param_idx));
        }

        // Execute with appropriate bindings
        let mut q = sqlx::query_as::<_, EntryRow>(&sql).bind(notebook_id);

        if let Some(ref topic) = query.topic {
            q = q.bind(topic);
        }

        if let Some(ref author_id) = query.author_id {
            q = q.bind(author_id.as_slice());
        }

        if let Some(after_sequence) = query.after_sequence {
            q = q.bind(after_sequence);
        }

        if let Some(limit) = query.limit {
            q = q.bind(limit);
        }

        Ok(q.fetch_all(&self.pool).await?)
    }

    /// Get entries referencing a specific entry.
    pub async fn get_entries_referencing(&self, entry_id: Uuid) -> StoreResult<Vec<EntryRow>> {
        Ok(sqlx::query_as::<_, EntryRow>(
            r#"
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM entries
            WHERE $1 = ANY("references")
            ORDER BY sequence
            "#,
        )
        .bind(entry_id)
        .fetch_all(&self.pool)
        .await?)
    }

    /// Get all revisions of an entry (revision chain).
    pub async fn get_revisions(&self, entry_id: Uuid) -> StoreResult<Vec<EntryRow>> {
        Ok(sqlx::query_as::<_, EntryRow>(
            r#"
            WITH RECURSIVE revision_chain AS (
                SELECT id, notebook_id, content, content_type, topic,
                       author_id, signature, revision_of, "references",
                       sequence, created, integration_cost, 1 as depth
                FROM entries
                WHERE revision_of = $1

                UNION ALL

                SELECT e.id, e.notebook_id, e.content, e.content_type, e.topic,
                       e.author_id, e.signature, e.revision_of, e."references",
                       e.sequence, e.created, e.integration_cost, rc.depth + 1
                FROM entries e
                JOIN revision_chain rc ON e.revision_of = rc.id
                WHERE rc.depth < 100  -- Prevent infinite loops
            )
            SELECT id, notebook_id, content, content_type, topic,
                   author_id, signature, revision_of, "references",
                   sequence, created, integration_cost
            FROM revision_chain
            ORDER BY depth
            "#,
        )
        .bind(entry_id)
        .fetch_all(&self.pool)
        .await?)
    }

    /// Get activity context for computing causal position.
    pub async fn get_activity_context(
        &self,
        notebook_id: Uuid,
        author_id: &[u8; 32],
    ) -> StoreResult<(u32, u32)> {
        // Get total entries in notebook
        let total: (i64,) =
            sqlx::query_as(r#"SELECT COUNT(*) FROM entries WHERE notebook_id = $1"#)
                .bind(notebook_id)
                .fetch_one(&self.pool)
                .await?;

        // Get entries since last by this author
        let last_by_author: (Option<i64>,) = sqlx::query_as(
            r#"
            SELECT MAX(sequence) FROM entries
            WHERE notebook_id = $1 AND author_id = $2
            "#,
        )
        .bind(notebook_id)
        .bind(author_id.as_slice())
        .fetch_one(&self.pool)
        .await?;

        let entries_since = match last_by_author.0 {
            Some(last_seq) => {
                let count: (i64,) = sqlx::query_as(
                    r#"
                    SELECT COUNT(*) FROM entries
                    WHERE notebook_id = $1 AND sequence > $2
                    "#,
                )
                .bind(notebook_id)
                .bind(last_seq)
                .fetch_one(&self.pool)
                .await?;
                count.0 as u32
            }
            None => total.0 as u32, // Author has no entries yet
        };

        Ok((entries_since, total.0 as u32))
    }

    // ==================== Entropy Operations ====================

    /// Get the recent entropy for a notebook (rolling sum of catalog_shift from last 10 entries).
    pub async fn get_recent_entropy(&self, notebook_id: Uuid) -> StoreResult<f64> {
        let result: (Option<f64>,) = sqlx::query_as(
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
        .bind(notebook_id)
        .fetch_one(&self.pool)
        .await?;

        Ok(result.0.unwrap_or(0.0))
    }

    // ==================== Graph Operations ====================

    /// Add an entry vertex and edges to the graph.
    async fn add_entry_to_graph(&self, entry: &EntryRow) -> StoreResult<()> {
        // Convert author_id to hex string for graph storage
        let author_hex: String = entry
            .author_id
            .iter()
            .map(|b| format!("{:02x}", b))
            .collect();

        // Add vertex
        sqlx::query("SELECT add_entry_vertex($1, $2, $3, $4, $5)")
            .bind(entry.id)
            .bind(entry.notebook_id)
            .bind(&entry.topic)
            .bind(&author_hex)
            .bind(entry.sequence)
            .execute(&self.pool)
            .await
            .map_err(|e| StoreError::GraphError(format!("Failed to add vertex: {}", e)))?;

        // Add reference edges
        for ref_id in &entry.references {
            sqlx::query("SELECT add_reference_edge($1, $2)")
                .bind(entry.id)
                .bind(ref_id)
                .execute(&self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("Failed to add reference edge: {}", e))
                })?;
        }

        // Add revision edge if applicable
        if let Some(revision_of) = entry.revision_of {
            sqlx::query("SELECT add_revision_edge($1, $2)")
                .bind(entry.id)
                .bind(revision_of)
                .execute(&self.pool)
                .await
                .map_err(|e| {
                    StoreError::GraphError(format!("Failed to add revision edge: {}", e))
                })?;
        }

        Ok(())
    }

    // ==================== User Operations ====================

    /// Insert a new user.
    pub async fn insert_user(&self, user: &NewUser) -> StoreResult<UserRow> {
        let row = sqlx::query_as::<_, UserRow>(
            r#"
            INSERT INTO users (username, display_name, password_hash, author_id, role)
            VALUES ($1, $2, $3, $4, $5)
            RETURNING id, username, display_name, password_hash, author_id, role, is_active, created, updated
            "#,
        )
        .bind(&user.username)
        .bind(&user.display_name)
        .bind(&user.password_hash)
        .bind(user.author_id.as_slice())
        .bind(&user.role)
        .fetch_one(&self.pool)
        .await?;

        Ok(row)
    }

    /// Get a user by ID.
    pub async fn get_user_by_id(&self, id: Uuid) -> StoreResult<UserRow> {
        sqlx::query_as::<_, UserRow>(
            r#"
            SELECT id, username, display_name, password_hash, author_id, role, is_active, created, updated
            FROM users WHERE id = $1
            "#,
        )
        .bind(id)
        .fetch_optional(&self.pool)
        .await?
        .ok_or_else(|| StoreError::ConfigError(format!("User not found: {}", id)))
    }

    /// Get a user by username.
    pub async fn get_user_by_username(&self, username: &str) -> StoreResult<Option<UserRow>> {
        Ok(sqlx::query_as::<_, UserRow>(
            r#"
            SELECT id, username, display_name, password_hash, author_id, role, is_active, created, updated
            FROM users WHERE username = $1
            "#,
        )
        .bind(username)
        .fetch_optional(&self.pool)
        .await?)
    }

    /// List all users.
    pub async fn list_users(&self) -> StoreResult<Vec<UserRow>> {
        Ok(sqlx::query_as::<_, UserRow>(
            r#"
            SELECT id, username, display_name, password_hash, author_id, role, is_active, created, updated
            FROM users ORDER BY created
            "#,
        )
        .fetch_all(&self.pool)
        .await?)
    }

    /// Update a user's display name and/or role.
    pub async fn update_user(
        &self,
        id: Uuid,
        display_name: Option<&str>,
        role: Option<&str>,
    ) -> StoreResult<UserRow> {
        let row = sqlx::query_as::<_, UserRow>(
            r#"
            UPDATE users SET
                display_name = COALESCE($2, display_name),
                role = COALESCE($3, role),
                updated = NOW()
            WHERE id = $1
            RETURNING id, username, display_name, password_hash, author_id, role, is_active, created, updated
            "#,
        )
        .bind(id)
        .bind(display_name)
        .bind(role)
        .fetch_optional(&self.pool)
        .await?
        .ok_or_else(|| StoreError::ConfigError(format!("User not found: {}", id)))?;

        Ok(row)
    }

    /// Update a user's password hash.
    pub async fn update_user_password(&self, id: Uuid, password_hash: &str) -> StoreResult<()> {
        let result =
            sqlx::query("UPDATE users SET password_hash = $2, updated = NOW() WHERE id = $1")
                .bind(id)
                .bind(password_hash)
                .execute(&self.pool)
                .await?;

        if result.rows_affected() == 0 {
            return Err(StoreError::ConfigError(format!("User not found: {}", id)));
        }
        Ok(())
    }

    /// Deactivate a user (soft delete).
    pub async fn deactivate_user(&self, id: Uuid) -> StoreResult<()> {
        let result =
            sqlx::query("UPDATE users SET is_active = false, updated = NOW() WHERE id = $1")
                .bind(id)
                .execute(&self.pool)
                .await?;

        if result.rows_affected() == 0 {
            return Err(StoreError::ConfigError(format!("User not found: {}", id)));
        }
        Ok(())
    }

    /// Check if any users exist.
    pub async fn has_users(&self) -> StoreResult<bool> {
        let result: (bool,) = sqlx::query_as("SELECT EXISTS (SELECT 1 FROM users)")
            .fetch_one(&self.pool)
            .await?;
        Ok(result.0)
    }

    // ==================== User Key Operations ====================

    /// Store a user's encrypted private key.
    pub async fn store_user_key(
        &self,
        user_id: Uuid,
        encrypted_private_key: &[u8],
    ) -> StoreResult<()> {
        sqlx::query(
            r#"
            INSERT INTO user_keys (user_id, encrypted_private_key)
            VALUES ($1, $2)
            ON CONFLICT (user_id) DO UPDATE SET encrypted_private_key = $2
            "#,
        )
        .bind(user_id)
        .bind(encrypted_private_key)
        .execute(&self.pool)
        .await?;
        Ok(())
    }

    /// Get a user's encrypted private key.
    pub async fn get_user_key(&self, user_id: Uuid) -> StoreResult<Option<UserKeyRow>> {
        Ok(sqlx::query_as::<_, UserKeyRow>(
            "SELECT user_id, encrypted_private_key FROM user_keys WHERE user_id = $1",
        )
        .bind(user_id)
        .fetch_optional(&self.pool)
        .await?)
    }

    // ==================== Quota Operations ====================

    /// Get a user's quota.
    pub async fn get_user_quota(&self, user_id: Uuid) -> StoreResult<Option<UserQuotaRow>> {
        Ok(sqlx::query_as::<_, UserQuotaRow>(
            r#"
            SELECT user_id, max_notebooks, max_entries_per_notebook, max_entry_size_bytes, max_total_storage_bytes
            FROM user_quotas WHERE user_id = $1
            "#,
        )
        .bind(user_id)
        .fetch_optional(&self.pool)
        .await?)
    }

    /// Create or update a user's quota.
    pub async fn upsert_user_quota(
        &self,
        user_id: Uuid,
        max_notebooks: i32,
        max_entries_per_notebook: i32,
        max_entry_size_bytes: i32,
        max_total_storage_bytes: i64,
    ) -> StoreResult<UserQuotaRow> {
        let row = sqlx::query_as::<_, UserQuotaRow>(
            r#"
            INSERT INTO user_quotas (user_id, max_notebooks, max_entries_per_notebook, max_entry_size_bytes, max_total_storage_bytes)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (user_id) DO UPDATE SET
                max_notebooks = $2,
                max_entries_per_notebook = $3,
                max_entry_size_bytes = $4,
                max_total_storage_bytes = $5
            RETURNING user_id, max_notebooks, max_entries_per_notebook, max_entry_size_bytes, max_total_storage_bytes
            "#,
        )
        .bind(user_id)
        .bind(max_notebooks)
        .bind(max_entries_per_notebook)
        .bind(max_entry_size_bytes)
        .bind(max_total_storage_bytes)
        .fetch_one(&self.pool)
        .await?;
        Ok(row)
    }

    /// Check notebook quota: returns (current_count, max_allowed).
    pub async fn check_notebook_quota(&self, user_id: Uuid) -> StoreResult<(i64, i32)> {
        let user = self.get_user_by_id(user_id).await?;

        let count: (i64,) = sqlx::query_as("SELECT COUNT(*) FROM notebooks WHERE owner_id = $1")
            .bind(&user.author_id)
            .fetch_one(&self.pool)
            .await?;

        let quota = self.get_user_quota(user_id).await?;
        let max = quota.map(|q| q.max_notebooks).unwrap_or(10);

        Ok((count.0, max))
    }

    /// Check entry quota for a notebook: returns (current_count, max_allowed).
    pub async fn check_entry_quota(
        &self,
        user_id: Uuid,
        notebook_id: Uuid,
    ) -> StoreResult<(i64, i32)> {
        let count: (i64,) = sqlx::query_as("SELECT COUNT(*) FROM entries WHERE notebook_id = $1")
            .bind(notebook_id)
            .fetch_one(&self.pool)
            .await?;

        let quota = self.get_user_quota(user_id).await?;
        let max = quota.map(|q| q.max_entries_per_notebook).unwrap_or(1000);

        Ok((count.0, max))
    }

    // ==================== Usage Log Operations ====================

    /// Log an action to the usage log.
    pub async fn log_action(&self, entry: &NewUsageLogEntry) -> StoreResult<()> {
        sqlx::query(
            r#"
            INSERT INTO usage_log (user_id, author_id, action, resource_type, resource_id, details, ip_address)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            "#,
        )
        .bind(entry.user_id)
        .bind(entry.author_id.as_slice())
        .bind(&entry.action)
        .bind(&entry.resource_type)
        .bind(&entry.resource_id)
        .bind(&entry.details)
        .bind(&entry.ip_address)
        .execute(&self.pool)
        .await?;
        Ok(())
    }

    /// Get usage log entries with optional filters.
    pub async fn get_usage_log(
        &self,
        user_id: Option<Uuid>,
        action: Option<&str>,
        resource_type: Option<&str>,
        resource_id: Option<&str>,
        limit: i64,
        offset: i64,
    ) -> StoreResult<Vec<UsageLogRow>> {
        let mut sql = String::from(
            "SELECT id, user_id, author_id, action, resource_type, resource_id, details, ip_address, created FROM usage_log WHERE 1=1",
        );
        let mut param_idx = 0;

        if user_id.is_some() {
            param_idx += 1;
            sql.push_str(&format!(" AND user_id = ${}", param_idx));
        }
        if action.is_some() {
            param_idx += 1;
            sql.push_str(&format!(" AND action = ${}", param_idx));
        }
        if resource_type.is_some() {
            param_idx += 1;
            sql.push_str(&format!(" AND resource_type = ${}", param_idx));
        }
        if resource_id.is_some() {
            param_idx += 1;
            sql.push_str(&format!(" AND resource_id = ${}", param_idx));
        }

        sql.push_str(&format!(
            " ORDER BY created DESC LIMIT ${} OFFSET ${}",
            param_idx + 1,
            param_idx + 2
        ));

        let mut q = sqlx::query_as::<_, UsageLogRow>(&sql);

        if let Some(uid) = user_id {
            q = q.bind(uid);
        }
        if let Some(a) = action {
            q = q.bind(a);
        }
        if let Some(rt) = resource_type {
            q = q.bind(rt);
        }
        if let Some(ri) = resource_id {
            q = q.bind(ri);
        }

        q = q.bind(limit).bind(offset);

        Ok(q.fetch_all(&self.pool).await?)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_config_default() {
        let config = StoreConfig::default();
        assert_eq!(config.max_connections, 10);
        assert_eq!(config.min_connections, 1);
        assert!(config.run_migrations);
    }
}
