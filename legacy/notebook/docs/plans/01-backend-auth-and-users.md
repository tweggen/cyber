# Plan 01: Backend Auth & Users

Migration, store layer, auth module, auth routes.

## Prerequisites

- Working PostgreSQL with existing schema (002_schema, 003_graph, 004_coherence_links)
- Rust workspace builds cleanly (`cargo build` from `notebook/`)

## Step 1: Add Dependencies to Workspace

### File: `notebook/Cargo.toml`

Add to `[workspace.dependencies]`:

```toml
# Authentication (added for user management)
argon2 = "0.5"
jsonwebtoken = "9"
```

### File: `notebook/crates/notebook-server/Cargo.toml`

Add to `[dependencies]`:

```toml
# Authentication
argon2 = { workspace = true }
jsonwebtoken = { workspace = true }
```

### File: `notebook/crates/notebook-store/Cargo.toml`

Add to `[dependencies]`:

```toml
# Authentication (for user model types)
argon2 = { workspace = true }
```

## Step 2: Database Migration

### File: `notebook/migrations/005_users.sql` (NEW)

```sql
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
```

### File: `notebook/crates/notebook-store/src/schema.rs`

Add the migration constant and register it in `run_migrations`:

```rust
// Add after existing constants:
pub const USERS_MIGRATION: &str = include_str!("../../../migrations/005_users.sql");
```

In `run_migrations()`, add after the coherence links migration block:

```rust
    // Run users migration
    tracing::debug!("Running users migration (005_users.sql)...");
    sqlx::raw_sql(USERS_MIGRATION)
        .execute(pool)
        .await
        .map_err(|e| StoreError::MigrationError(format!("Users migration failed: {}", e)))?;
```

Update `get_schema_version()` to detect the users table (return 4 if found):

After the graph functions check, before the final `Ok(...)`:

```rust
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

    if has_users.0 {
        return Ok(4);
    }
```

Update the existing schema test:

```rust
    #[test]
    fn test_users_migration_embedded() {
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS users"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS user_keys"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS user_quotas"));
        assert!(USERS_MIGRATION.contains("CREATE TABLE IF NOT EXISTS usage_log"));
    }
```

## Step 3: Store Layer — New Models

### File: `notebook/crates/notebook-store/src/models.rs`

Add these types at the end of the file (before any test modules):

```rust
// ==================== User Management Models ====================

/// Database row for the `users` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserRow {
    pub id: Uuid,
    pub username: String,
    pub display_name: Option<String>,
    pub password_hash: String,
    pub author_id: Vec<u8>,
    pub role: String,
    pub is_active: bool,
    pub created: DateTime<Utc>,
    pub updated: DateTime<Utc>,
}

/// Input for creating a new user.
#[derive(Debug, Clone)]
pub struct NewUser {
    pub username: String,
    pub display_name: Option<String>,
    pub password_hash: String,
    pub author_id: [u8; 32],
    pub role: String,
}

/// Database row for the `user_keys` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserKeyRow {
    pub user_id: Uuid,
    pub encrypted_private_key: Vec<u8>,
}

/// Database row for the `user_quotas` table.
#[derive(Debug, Clone, FromRow)]
pub struct UserQuotaRow {
    pub user_id: Uuid,
    pub max_notebooks: i32,
    pub max_entries_per_notebook: i32,
    pub max_entry_size_bytes: i32,
    pub max_total_storage_bytes: i64,
}

/// Database row for the `usage_log` table.
#[derive(Debug, Clone, FromRow)]
pub struct UsageLogRow {
    pub id: i64,
    pub user_id: Option<Uuid>,
    pub author_id: Vec<u8>,
    pub action: String,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    pub details: Option<serde_json::Value>,
    pub ip_address: Option<String>,
    pub created: DateTime<Utc>,
}

/// Input for creating a new usage log entry.
#[derive(Debug, Clone)]
pub struct NewUsageLogEntry {
    pub user_id: Option<Uuid>,
    pub author_id: [u8; 32],
    pub action: String,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    pub details: Option<serde_json::Value>,
    pub ip_address: Option<String>,
}
```

## Step 4: Store Layer — New Operations

### File: `notebook/crates/notebook-store/src/store.rs`

Add these methods inside `impl Store { ... }`, after the existing "Graph Operations" section:

```rust
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
        let result = sqlx::query(
            "UPDATE users SET password_hash = $2, updated = NOW() WHERE id = $1",
        )
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
        let result = sqlx::query(
            "UPDATE users SET is_active = false, updated = NOW() WHERE id = $1",
        )
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
        let result: (bool,) = sqlx::query_as(
            "SELECT EXISTS (SELECT 1 FROM users)",
        )
        .fetch_one(&self.pool)
        .await?;
        Ok(result.0)
    }

    // ==================== User Key Operations ====================

    /// Store a user's encrypted private key.
    pub async fn store_user_key(&self, user_id: Uuid, encrypted_private_key: &[u8]) -> StoreResult<()> {
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
        // Get the user's author_id
        let user = self.get_user_by_id(user_id).await?;

        let count: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM notebooks WHERE owner_id = $1",
        )
        .bind(&user.author_id)
        .fetch_one(&self.pool)
        .await?;

        let quota = self.get_user_quota(user_id).await?;
        let max = quota.map(|q| q.max_notebooks).unwrap_or(10);

        Ok((count.0, max))
    }

    /// Check entry quota for a notebook: returns (current_count, max_allowed).
    pub async fn check_entry_quota(&self, user_id: Uuid, notebook_id: Uuid) -> StoreResult<(i64, i32)> {
        let count: (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM entries WHERE notebook_id = $1",
        )
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
        // Build dynamic query
        let mut sql = String::from(
            "SELECT id, user_id, author_id, action, resource_type, resource_id, details, ip_address, created FROM usage_log WHERE 1=1"
        );
        let mut param_idx = 1;

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

        sql.push_str(&format!(" ORDER BY created DESC LIMIT ${} OFFSET ${}", param_idx + 1, param_idx + 2));

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
```

## Step 5: Auth Module

### File: `notebook/crates/notebook-server/src/auth.rs` (NEW)

```rust
//! Authentication module: JWT token management and password hashing.

use argon2::{
    password_hash::{rand_core::OsRng, PasswordHash, PasswordHasher, PasswordVerifier, SaltString},
    Argon2,
};
use axum::{
    extract::{FromRequestParts, State},
    http::{header, request::Parts, StatusCode},
};
use jsonwebtoken::{decode, encode, DecodingKey, EncodingKey, Header, Validation};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::error::ApiError;
use crate::state::AppState;

/// JWT claims.
#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct Claims {
    /// User ID (subject).
    pub sub: Uuid,
    /// Author ID as hex string.
    pub author_id: String,
    /// User role ("admin" or "user").
    pub role: String,
    /// Expiration time (unix timestamp).
    pub exp: usize,
    /// Issued at (unix timestamp).
    pub iat: usize,
}

/// Authenticated user extracted from JWT.
#[derive(Debug, Clone)]
pub struct AuthenticatedUser {
    /// User ID.
    pub user_id: Uuid,
    /// Author ID as 32-byte array.
    pub author_id: [u8; 32],
    /// Author ID as hex string.
    pub author_id_hex: String,
    /// User role.
    pub role: String,
}

impl AuthenticatedUser {
    /// Check if user is admin.
    pub fn is_admin(&self) -> bool {
        self.role == "admin"
    }

    /// Check if user is the given user_id or is admin.
    pub fn is_self_or_admin(&self, user_id: Uuid) -> bool {
        self.user_id == user_id || self.is_admin()
    }
}

/// Create a JWT token for a user.
pub fn create_token(
    user_id: Uuid,
    author_id_hex: &str,
    role: &str,
    secret: &str,
    expiry_hours: u64,
) -> Result<String, ApiError> {
    let now = chrono::Utc::now();
    let exp = (now + chrono::Duration::hours(expiry_hours as i64)).timestamp() as usize;

    let claims = Claims {
        sub: user_id,
        author_id: author_id_hex.to_string(),
        role: role.to_string(),
        exp,
        iat: now.timestamp() as usize,
    };

    encode(
        &Header::default(),
        &claims,
        &EncodingKey::from_secret(secret.as_bytes()),
    )
    .map_err(|e| ApiError::Internal(format!("Failed to create token: {}", e)))
}

/// Validate a JWT token and return claims.
pub fn validate_token(token: &str, secret: &str) -> Result<Claims, ApiError> {
    let token_data = decode::<Claims>(
        token,
        &DecodingKey::from_secret(secret.as_bytes()),
        &Validation::default(),
    )
    .map_err(|e| ApiError::Unauthorized(format!("Invalid token: {}", e)))?;

    Ok(token_data.claims)
}

/// Hash a password using Argon2.
pub fn hash_password(password: &str) -> Result<String, ApiError> {
    let salt = SaltString::generate(&mut OsRng);
    let argon2 = Argon2::default();
    let password_hash = argon2
        .hash_password(password.as_bytes(), &salt)
        .map_err(|e| ApiError::Internal(format!("Failed to hash password: {}", e)))?;
    Ok(password_hash.to_string())
}

/// Verify a password against a hash.
pub fn verify_password(password: &str, hash: &str) -> Result<bool, ApiError> {
    let parsed_hash = PasswordHash::new(hash)
        .map_err(|e| ApiError::Internal(format!("Invalid password hash: {}", e)))?;
    Ok(Argon2::default()
        .verify_password(password.as_bytes(), &parsed_hash)
        .is_ok())
}

/// Parse a hex author_id string to [u8; 32].
fn parse_author_id_hex(hex_str: &str) -> Result<[u8; 32], ApiError> {
    let bytes = hex::decode(hex_str)
        .map_err(|e| ApiError::Internal(format!("Invalid author_id hex: {}", e)))?;
    if bytes.len() != 32 {
        return Err(ApiError::Internal(format!(
            "author_id must be 32 bytes, got {}",
            bytes.len()
        )));
    }
    let mut arr = [0u8; 32];
    arr.copy_from_slice(&bytes);
    Ok(arr)
}

/// Axum extractor for AuthenticatedUser from JWT Bearer token.
///
/// Usage in handlers:
/// ```ignore
/// async fn my_handler(user: AuthenticatedUser) -> ... { ... }
/// ```
impl FromRequestParts<AppState> for AuthenticatedUser {
    type Rejection = ApiError;

    async fn from_request_parts(
        parts: &mut Parts,
        state: &AppState,
    ) -> Result<Self, Self::Rejection> {
        let auth_header = parts
            .headers
            .get(header::AUTHORIZATION)
            .and_then(|v| v.to_str().ok())
            .ok_or_else(|| ApiError::Unauthorized("Missing Authorization header".to_string()))?;

        let token = auth_header
            .strip_prefix("Bearer ")
            .ok_or_else(|| {
                ApiError::Unauthorized("Authorization header must be Bearer <token>".to_string())
            })?;

        let jwt_secret = &state.config().jwt_secret;
        let claims = validate_token(token, jwt_secret)?;

        let author_id = parse_author_id_hex(&claims.author_id)?;

        Ok(AuthenticatedUser {
            user_id: claims.sub,
            author_id,
            author_id_hex: claims.author_id,
            role: claims.role,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_hash_and_verify_password() {
        let password = "test_password_123";
        let hash = hash_password(password).unwrap();
        assert!(verify_password(password, &hash).unwrap());
        assert!(!verify_password("wrong_password", &hash).unwrap());
    }

    #[test]
    fn test_create_and_validate_token() {
        let secret = "test_secret_key_12345";
        let user_id = Uuid::new_v4();
        let author_hex = "00".repeat(32);

        let token = create_token(user_id, &author_hex, "admin", secret, 24).unwrap();
        let claims = validate_token(&token, secret).unwrap();

        assert_eq!(claims.sub, user_id);
        assert_eq!(claims.author_id, author_hex);
        assert_eq!(claims.role, "admin");
    }

    #[test]
    fn test_validate_token_wrong_secret() {
        let token = create_token(Uuid::new_v4(), &"00".repeat(32), "user", "secret1", 24).unwrap();
        let result = validate_token(&token, "secret2");
        assert!(result.is_err());
    }

    #[test]
    fn test_parse_author_id_hex() {
        let hex = "00".repeat(32);
        let result = parse_author_id_hex(&hex).unwrap();
        assert_eq!(result, [0u8; 32]);
    }

    #[test]
    fn test_authenticated_user_is_admin() {
        let user = AuthenticatedUser {
            user_id: Uuid::new_v4(),
            author_id: [0u8; 32],
            author_id_hex: "00".repeat(32),
            role: "admin".to_string(),
        };
        assert!(user.is_admin());
    }

    #[test]
    fn test_authenticated_user_is_self_or_admin() {
        let uid = Uuid::new_v4();
        let user = AuthenticatedUser {
            user_id: uid,
            author_id: [0u8; 32],
            author_id_hex: "00".repeat(32),
            role: "user".to_string(),
        };
        assert!(user.is_self_or_admin(uid));
        assert!(!user.is_self_or_admin(Uuid::new_v4()));
    }
}
```

## Step 6: Update Config

### File: `notebook/crates/notebook-server/src/config.rs`

Add JWT and admin bootstrap fields to `ServerConfig`:

```rust
pub struct ServerConfig {
    pub database_url: String,
    pub port: u16,
    pub log_level: String,
    pub cors_allowed_origins: String,
    /// JWT signing secret. Required in production, auto-generated in dev.
    pub jwt_secret: String,
    /// JWT token expiry in hours. Default: 24.
    pub jwt_expiry_hours: u64,
    /// Admin username for first-run bootstrap.
    pub admin_username: Option<String>,
    /// Admin password for first-run bootstrap.
    pub admin_password: Option<String>,
}
```

Update `from_env()` to read the new fields:

```rust
        let jwt_secret = env::var("JWT_SECRET").unwrap_or_else(|_| {
            let secret: String = (0..64)
                .map(|_| format!("{:02x}", rand::random::<u8>()))
                .collect();
            tracing::warn!("JWT_SECRET not set, using auto-generated secret (not suitable for production)");
            secret
        });

        let jwt_expiry_hours = env::var("JWT_EXPIRY_HOURS")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(24);

        let admin_username = env::var("ADMIN_USERNAME").ok();
        let admin_password = env::var("ADMIN_PASSWORD").ok();
```

Add `rand` dependency to notebook-server Cargo.toml:

```toml
rand = { workspace = true }
```

## Step 7: Register Auth Module

### File: `notebook/crates/notebook-server/src/lib.rs`

Add the auth module:

```rust
pub mod auth;
pub mod config;
pub mod error;
pub mod events;
pub mod middleware;
pub mod routes;
pub mod state;

// Re-exports
pub use auth::AuthenticatedUser;
pub use config::{ConfigError, ServerConfig};
pub use error::{ApiError, ApiResult};
pub use events::EventBroadcaster;
pub use state::AppState;
```

## Step 8: Auth Routes

### File: `notebook/crates/notebook-server/src/routes/auth.rs` (NEW)

```rust
//! Authentication routes: login, logout, me, change-password.

use axum::{
    extract::State,
    http::StatusCode,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::auth::{self, AuthenticatedUser};
use crate::error::{ApiError, ApiResult};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

#[derive(Debug, Deserialize)]
pub struct LoginRequest {
    pub username: String,
    pub password: String,
}

#[derive(Debug, Serialize)]
pub struct LoginResponse {
    pub token: String,
    pub user_id: Uuid,
    pub username: String,
    pub role: String,
    pub expires_in_hours: u64,
}

#[derive(Debug, Serialize)]
pub struct MeResponse {
    pub user_id: Uuid,
    pub username: String,
    pub display_name: Option<String>,
    pub role: String,
    pub author_id: String,
}

#[derive(Debug, Deserialize)]
pub struct ChangePasswordRequest {
    pub current_password: String,
    pub new_password: String,
}

#[derive(Debug, Serialize)]
pub struct ChangePasswordResponse {
    pub message: String,
}

// ============================================================================
// Route Handlers
// ============================================================================

/// POST /api/auth/login
async fn login(
    State(state): State<AppState>,
    Json(request): Json<LoginRequest>,
) -> ApiResult<Json<LoginResponse>> {
    let store = state.store();

    // Find user by username
    let user = store
        .get_user_by_username(&request.username)
        .await?
        .ok_or_else(|| ApiError::Unauthorized("Invalid username or password".to_string()))?;

    // Check if active
    if !user.is_active {
        return Err(ApiError::Unauthorized("Account is deactivated".to_string()));
    }

    // Verify password
    let valid = auth::verify_password(&request.password, &user.password_hash)?;
    if !valid {
        return Err(ApiError::Unauthorized("Invalid username or password".to_string()));
    }

    // Generate author_id hex
    let author_id_hex: String = user.author_id.iter().map(|b| format!("{:02x}", b)).collect();

    // Create JWT
    let config = state.config();
    let token = auth::create_token(
        user.id,
        &author_id_hex,
        &user.role,
        &config.jwt_secret,
        config.jwt_expiry_hours,
    )?;

    tracing::info!(user_id = %user.id, username = %user.username, "User logged in");

    Ok(Json(LoginResponse {
        token,
        user_id: user.id,
        username: user.username,
        role: user.role,
        expires_in_hours: config.jwt_expiry_hours,
    }))
}

/// POST /api/auth/logout — informational (client discards token).
async fn logout(user: AuthenticatedUser) -> ApiResult<StatusCode> {
    tracing::info!(user_id = %user.user_id, "User logged out");
    Ok(StatusCode::NO_CONTENT)
}

/// GET /api/auth/me — current user info.
async fn me(
    State(state): State<AppState>,
    user: AuthenticatedUser,
) -> ApiResult<Json<MeResponse>> {
    let store = state.store();
    let user_row = store.get_user_by_id(user.user_id).await?;

    Ok(Json(MeResponse {
        user_id: user_row.id,
        username: user_row.username,
        display_name: user_row.display_name,
        role: user_row.role,
        author_id: user.author_id_hex,
    }))
}

/// POST /api/auth/change-password
async fn change_password(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Json(request): Json<ChangePasswordRequest>,
) -> ApiResult<Json<ChangePasswordResponse>> {
    let store = state.store();

    // Fetch current user
    let user_row = store.get_user_by_id(user.user_id).await?;

    // Verify current password
    let valid = auth::verify_password(&request.current_password, &user_row.password_hash)?;
    if !valid {
        return Err(ApiError::Unauthorized("Current password is incorrect".to_string()));
    }

    // Validate new password
    if request.new_password.len() < 8 {
        return Err(ApiError::BadRequest("New password must be at least 8 characters".to_string()));
    }

    // Hash and store new password
    let new_hash = auth::hash_password(&request.new_password)?;
    store.update_user_password(user.user_id, &new_hash).await?;

    tracing::info!(user_id = %user.user_id, "Password changed");

    Ok(Json(ChangePasswordResponse {
        message: "Password changed successfully".to_string(),
    }))
}

/// Build auth routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/api/auth/login", post(login))
        .route("/api/auth/logout", post(logout))
        .route("/api/auth/me", get(me))
        .route("/api/auth/change-password", post(change_password))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_login_request_deserialize() {
        let json = r#"{"username": "admin", "password": "secret"}"#;
        let request: LoginRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.username, "admin");
        assert_eq!(request.password, "secret");
    }

    #[test]
    fn test_login_response_serialize() {
        let response = LoginResponse {
            token: "jwt.token.here".to_string(),
            user_id: Uuid::nil(),
            username: "admin".to_string(),
            role: "admin".to_string(),
            expires_in_hours: 24,
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("token"));
        assert!(json.contains("user_id"));
    }

    #[test]
    fn test_change_password_request_deserialize() {
        let json = r#"{"current_password": "old", "new_password": "newpass123"}"#;
        let request: ChangePasswordRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.current_password, "old");
        assert_eq!(request.new_password, "newpass123");
    }
}
```

## Step 9: Register Auth Routes

### File: `notebook/crates/notebook-server/src/routes/mod.rs`

Update to:

```rust
//! Route definitions for the HTTP API.

pub mod auth;
pub mod browse;
pub mod entries;
pub mod events;
pub mod health;
pub mod notebooks;
pub mod observe;
pub mod share;

use axum::Router;

use crate::state::AppState;

/// Build the complete router with all routes.
pub fn build_router(state: AppState) -> Router {
    Router::new()
        .merge(health::routes())
        .merge(auth::routes())
        .merge(entries::routes())
        .merge(notebooks::routes())
        .merge(observe::routes())
        .merge(share::routes())
        .merge(events::routes())
        .merge(browse::routes())
        .with_state(state)
}
```

## Verification

After implementing all changes:

1. `cargo build` from `notebook/` — should compile with new dependencies
2. `cargo test` from `notebook/` — existing tests still pass, new auth tests pass
3. `cargo clippy -- -D warnings` — no warnings
4. Manual test: start server with `ADMIN_USERNAME` and `ADMIN_PASSWORD` env vars
5. Verify `POST /api/auth/login` returns a JWT
6. Verify `GET /api/auth/me` with the JWT returns user info

## Files Changed Summary

| File | Action |
|------|--------|
| `notebook/Cargo.toml` | Modified — add argon2, jsonwebtoken |
| `notebook/crates/notebook-server/Cargo.toml` | Modified — add argon2, jsonwebtoken, rand |
| `notebook/crates/notebook-store/Cargo.toml` | Modified — add argon2 |
| `notebook/migrations/005_users.sql` | **New** |
| `notebook/crates/notebook-store/src/schema.rs` | Modified — register migration |
| `notebook/crates/notebook-store/src/models.rs` | Modified — add user/quota/usage models |
| `notebook/crates/notebook-store/src/store.rs` | Modified — add user/quota/usage operations |
| `notebook/crates/notebook-server/src/config.rs` | Modified — add JWT/admin config |
| `notebook/crates/notebook-server/src/auth.rs` | **New** |
| `notebook/crates/notebook-server/src/lib.rs` | Modified — register auth module |
| `notebook/crates/notebook-server/src/routes/auth.rs` | **New** |
| `notebook/crates/notebook-server/src/routes/mod.rs` | Modified — register auth routes |
