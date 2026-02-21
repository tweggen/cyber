# Plan 02: Backend Management API

User management routes, usage log API, updating existing routes to require auth, admin bootstrap.

## Prerequisites

- Plan 01 fully implemented (migration, store layer, auth module, auth routes)
- `cargo build` succeeds
- Auth module (`auth.rs`) and `AuthenticatedUser` extractor working

## Step 1: User Management Routes

### File: `notebook/crates/notebook-server/src/routes/users.rs` (NEW)

```rust
//! User management routes: CRUD, quotas.

use axum::{
    extract::{Path, State},
    http::StatusCode,
    routing::{delete, get, post, put},
    Json, Router,
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use notebook_core::crypto::KeyPair;
use notebook_core::identity::derive_author_id;
use notebook_store::{NewAuthor, NewUser, NewUsageLogEntry};

use crate::auth::{self, AuthenticatedUser};
use crate::error::{ApiError, ApiResult};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

#[derive(Debug, Serialize)]
pub struct UserResponse {
    pub id: Uuid,
    pub username: String,
    pub display_name: Option<String>,
    pub role: String,
    pub author_id: String,
    pub is_active: bool,
    pub created: DateTime<Utc>,
    pub updated: DateTime<Utc>,
}

#[derive(Debug, Serialize)]
pub struct UsersListResponse {
    pub users: Vec<UserResponse>,
}

#[derive(Debug, Deserialize)]
pub struct CreateUserRequest {
    pub username: String,
    pub password: String,
    pub display_name: Option<String>,
    pub role: Option<String>,
}

#[derive(Debug, Deserialize)]
pub struct UpdateUserRequest {
    pub display_name: Option<String>,
    pub role: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct QuotaResponse {
    pub user_id: Uuid,
    pub max_notebooks: i32,
    pub max_entries_per_notebook: i32,
    pub max_entry_size_bytes: i32,
    pub max_total_storage_bytes: i64,
}

#[derive(Debug, Deserialize)]
pub struct UpdateQuotaRequest {
    pub max_notebooks: Option<i32>,
    pub max_entries_per_notebook: Option<i32>,
    pub max_entry_size_bytes: Option<i32>,
    pub max_total_storage_bytes: Option<i64>,
}

// ============================================================================
// Helpers
// ============================================================================

fn author_id_to_hex(bytes: &[u8]) -> String {
    bytes.iter().map(|b| format!("{:02x}", b)).collect()
}

fn user_row_to_response(row: &notebook_store::UserRow) -> UserResponse {
    UserResponse {
        id: row.id,
        username: row.username.clone(),
        display_name: row.display_name.clone(),
        role: row.role.clone(),
        author_id: author_id_to_hex(&row.author_id),
        is_active: row.is_active,
        created: row.created,
        updated: row.updated,
    }
}

// ============================================================================
// Route Handlers
// ============================================================================

/// GET /api/users — list all users (admin only).
async fn list_users(
    State(state): State<AppState>,
    user: AuthenticatedUser,
) -> ApiResult<Json<UsersListResponse>> {
    if !user.is_admin() {
        return Err(ApiError::Forbidden("Admin access required".to_string()));
    }

    let rows = state.store().list_users().await?;
    let users: Vec<UserResponse> = rows.iter().map(user_row_to_response).collect();

    Ok(Json(UsersListResponse { users }))
}

/// POST /api/users — create a new user (admin only).
///
/// Generates an Ed25519 keypair, registers the author, and creates the user.
async fn create_user(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Json(request): Json<CreateUserRequest>,
) -> ApiResult<(StatusCode, Json<UserResponse>)> {
    if !user.is_admin() {
        return Err(ApiError::Forbidden("Admin access required".to_string()));
    }

    // Validate
    if request.username.trim().is_empty() {
        return Err(ApiError::BadRequest("Username cannot be empty".to_string()));
    }
    if request.password.len() < 8 {
        return Err(ApiError::BadRequest("Password must be at least 8 characters".to_string()));
    }

    let store = state.store();

    // Check username uniqueness
    if store.get_user_by_username(&request.username).await?.is_some() {
        return Err(ApiError::BadRequest(format!(
            "Username '{}' is already taken",
            request.username
        )));
    }

    // Generate Ed25519 keypair
    let keypair = KeyPair::generate();
    let public_key = keypair.public_key();
    let author_id = derive_author_id(&public_key);

    // Register author in authors table
    let new_author = NewAuthor::new(*author_id.as_bytes(), *public_key.as_bytes());
    store.insert_author(&new_author).await.map_err(|e| {
        tracing::error!(error = %e, "Failed to create author");
        ApiError::Internal("Failed to create author".to_string())
    })?;

    // Hash password
    let password_hash = auth::hash_password(&request.password)?;

    // Create user
    let role = request.role.unwrap_or_else(|| "user".to_string());
    if role != "admin" && role != "user" {
        return Err(ApiError::BadRequest("Role must be 'admin' or 'user'".to_string()));
    }

    let new_user = NewUser {
        username: request.username.clone(),
        display_name: request.display_name,
        password_hash,
        author_id: *author_id.as_bytes(),
        role,
    };

    let user_row = store.insert_user(&new_user).await?;

    // Store the encrypted private key (for now, stored as raw bytes — in production,
    // encrypt with a server-side key derived from JWT_SECRET or a separate key)
    store
        .store_user_key(user_row.id, keypair.secret_key_bytes())
        .await?;

    // Create default quota
    store
        .upsert_user_quota(user_row.id, 10, 1000, 1_048_576, 104_857_600)
        .await?;

    // Log the action
    store
        .log_action(&NewUsageLogEntry {
            user_id: Some(user.user_id),
            author_id: user.author_id,
            action: "create_user".to_string(),
            resource_type: Some("user".to_string()),
            resource_id: Some(user_row.id.to_string()),
            details: None,
            ip_address: None,
        })
        .await
        .ok(); // Best effort

    tracing::info!(
        new_user_id = %user_row.id,
        username = %user_row.username,
        created_by = %user.user_id,
        "User created"
    );

    Ok((StatusCode::CREATED, Json(user_row_to_response(&user_row))))
}

/// GET /api/users/{id} — get user detail (admin or self).
async fn get_user(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(id): Path<Uuid>,
) -> ApiResult<Json<UserResponse>> {
    if !user.is_self_or_admin(id) {
        return Err(ApiError::Forbidden("Access denied".to_string()));
    }

    let row = state.store().get_user_by_id(id).await?;
    Ok(Json(user_row_to_response(&row)))
}

/// PUT /api/users/{id} — update user (admin or self for display_name only).
async fn update_user(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(id): Path<Uuid>,
    Json(request): Json<UpdateUserRequest>,
) -> ApiResult<Json<UserResponse>> {
    // Non-admin users can only update their own display_name
    if !user.is_admin() {
        if user.user_id != id {
            return Err(ApiError::Forbidden("Access denied".to_string()));
        }
        if request.role.is_some() {
            return Err(ApiError::Forbidden("Only admins can change roles".to_string()));
        }
    }

    // Validate role if provided
    if let Some(ref role) = request.role {
        if role != "admin" && role != "user" {
            return Err(ApiError::BadRequest("Role must be 'admin' or 'user'".to_string()));
        }
    }

    let row = state
        .store()
        .update_user(id, request.display_name.as_deref(), request.role.as_deref())
        .await?;

    Ok(Json(user_row_to_response(&row)))
}

/// DELETE /api/users/{id} — deactivate user (admin only).
async fn deactivate_user(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(id): Path<Uuid>,
) -> ApiResult<StatusCode> {
    if !user.is_admin() {
        return Err(ApiError::Forbidden("Admin access required".to_string()));
    }

    // Prevent self-deactivation
    if user.user_id == id {
        return Err(ApiError::BadRequest("Cannot deactivate your own account".to_string()));
    }

    state.store().deactivate_user(id).await?;

    // Log the action
    state
        .store()
        .log_action(&NewUsageLogEntry {
            user_id: Some(user.user_id),
            author_id: user.author_id,
            action: "deactivate_user".to_string(),
            resource_type: Some("user".to_string()),
            resource_id: Some(id.to_string()),
            details: None,
            ip_address: None,
        })
        .await
        .ok();

    tracing::info!(
        target_user_id = %id,
        deactivated_by = %user.user_id,
        "User deactivated"
    );

    Ok(StatusCode::NO_CONTENT)
}

/// GET /api/users/{id}/quota — view quota (admin or self).
async fn get_quota(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(id): Path<Uuid>,
) -> ApiResult<Json<QuotaResponse>> {
    if !user.is_self_or_admin(id) {
        return Err(ApiError::Forbidden("Access denied".to_string()));
    }

    let quota = state
        .store()
        .get_user_quota(id)
        .await?
        .ok_or_else(|| ApiError::NotFound("Quota not found".to_string()))?;

    Ok(Json(QuotaResponse {
        user_id: quota.user_id,
        max_notebooks: quota.max_notebooks,
        max_entries_per_notebook: quota.max_entries_per_notebook,
        max_entry_size_bytes: quota.max_entry_size_bytes,
        max_total_storage_bytes: quota.max_total_storage_bytes,
    }))
}

/// PUT /api/users/{id}/quota — update quota (admin only).
async fn update_quota(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(id): Path<Uuid>,
    Json(request): Json<UpdateQuotaRequest>,
) -> ApiResult<Json<QuotaResponse>> {
    if !user.is_admin() {
        return Err(ApiError::Forbidden("Admin access required".to_string()));
    }

    // Get current quota or defaults
    let current = state.store().get_user_quota(id).await?;
    let max_notebooks = request
        .max_notebooks
        .unwrap_or_else(|| current.as_ref().map(|q| q.max_notebooks).unwrap_or(10));
    let max_entries = request
        .max_entries_per_notebook
        .unwrap_or_else(|| current.as_ref().map(|q| q.max_entries_per_notebook).unwrap_or(1000));
    let max_entry_size = request
        .max_entry_size_bytes
        .unwrap_or_else(|| current.as_ref().map(|q| q.max_entry_size_bytes).unwrap_or(1_048_576));
    let max_storage = request
        .max_total_storage_bytes
        .unwrap_or_else(|| current.as_ref().map(|q| q.max_total_storage_bytes).unwrap_or(104_857_600));

    let quota = state
        .store()
        .upsert_user_quota(id, max_notebooks, max_entries, max_entry_size, max_storage)
        .await?;

    Ok(Json(QuotaResponse {
        user_id: quota.user_id,
        max_notebooks: quota.max_notebooks,
        max_entries_per_notebook: quota.max_entries_per_notebook,
        max_entry_size_bytes: quota.max_entry_size_bytes,
        max_total_storage_bytes: quota.max_total_storage_bytes,
    }))
}

/// Build user management routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/api/users", get(list_users).post(create_user))
        .route("/api/users/{id}", get(get_user).put(update_user).delete(deactivate_user))
        .route("/api/users/{id}/quota", get(get_quota).put(update_quota))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_create_user_request_deserialize() {
        let json = r#"{"username": "alice", "password": "secret123", "display_name": "Alice"}"#;
        let request: CreateUserRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.username, "alice");
        assert_eq!(request.password, "secret123");
        assert_eq!(request.display_name, Some("Alice".to_string()));
        assert!(request.role.is_none());
    }

    #[test]
    fn test_update_user_request_deserialize() {
        let json = r#"{"display_name": "Bob", "role": "admin"}"#;
        let request: UpdateUserRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.display_name, Some("Bob".to_string()));
        assert_eq!(request.role, Some("admin".to_string()));
    }

    #[test]
    fn test_update_quota_request_deserialize_partial() {
        let json = r#"{"max_notebooks": 20}"#;
        let request: UpdateQuotaRequest = serde_json::from_str(json).unwrap();
        assert_eq!(request.max_notebooks, Some(20));
        assert!(request.max_entries_per_notebook.is_none());
    }

    #[test]
    fn test_user_response_serialize() {
        let response = UserResponse {
            id: Uuid::nil(),
            username: "test".to_string(),
            display_name: None,
            role: "user".to_string(),
            author_id: "00".repeat(32),
            is_active: true,
            created: chrono::Utc::now(),
            updated: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("username"));
        assert!(json.contains("role"));
    }
}
```

## Step 2: Usage Log Routes

### File: `notebook/crates/notebook-server/src/routes/usage.rs` (NEW)

```rust
//! Usage log routes: query audit trail.

use axum::{
    extract::{Path, Query, State},
    routing::get,
    Json, Router,
};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use crate::auth::AuthenticatedUser;
use crate::error::{ApiError, ApiResult};
use crate::state::AppState;

// ============================================================================
// Request/Response Types
// ============================================================================

#[derive(Debug, Deserialize)]
pub struct UsageLogParams {
    pub user_id: Option<Uuid>,
    pub action: Option<String>,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    #[serde(default = "default_limit")]
    pub limit: i64,
    #[serde(default)]
    pub offset: i64,
}

fn default_limit() -> i64 {
    100
}

#[derive(Debug, Serialize)]
pub struct UsageLogEntry {
    pub id: i64,
    pub user_id: Option<Uuid>,
    pub author_id: String,
    pub action: String,
    pub resource_type: Option<String>,
    pub resource_id: Option<String>,
    pub details: Option<serde_json::Value>,
    pub ip_address: Option<String>,
    pub created: DateTime<Utc>,
}

#[derive(Debug, Serialize)]
pub struct UsageLogResponse {
    pub entries: Vec<UsageLogEntry>,
}

// ============================================================================
// Helpers
// ============================================================================

fn author_id_to_hex(bytes: &[u8]) -> String {
    bytes.iter().map(|b| format!("{:02x}", b)).collect()
}

// ============================================================================
// Route Handlers
// ============================================================================

/// GET /api/usage — query usage log (admin sees all, user sees own).
async fn get_usage_log(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Query(params): Query<UsageLogParams>,
) -> ApiResult<Json<UsageLogResponse>> {
    // Non-admin users can only see their own usage
    let filter_user_id = if user.is_admin() {
        params.user_id
    } else {
        Some(user.user_id)
    };

    let limit = params.limit.min(1000).max(1);
    let offset = params.offset.max(0);

    let rows = state
        .store()
        .get_usage_log(
            filter_user_id,
            params.action.as_deref(),
            params.resource_type.as_deref(),
            params.resource_id.as_deref(),
            limit,
            offset,
        )
        .await?;

    let entries: Vec<UsageLogEntry> = rows
        .iter()
        .map(|row| UsageLogEntry {
            id: row.id,
            user_id: row.user_id,
            author_id: author_id_to_hex(&row.author_id),
            action: row.action.clone(),
            resource_type: row.resource_type.clone(),
            resource_id: row.resource_id.clone(),
            details: row.details.clone(),
            ip_address: row.ip_address.clone(),
            created: row.created,
        })
        .collect();

    Ok(Json(UsageLogResponse { entries }))
}

/// GET /api/notebooks/{id}/usage — per-notebook usage log.
async fn get_notebook_usage(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(notebook_id): Path<Uuid>,
    Query(params): Query<UsageLogParams>,
) -> ApiResult<Json<UsageLogResponse>> {
    // Verify the notebook exists and user has access
    let store = state.store();
    store.get_notebook(notebook_id).await.map_err(|e| match e {
        notebook_store::StoreError::NotebookNotFound(id) => {
            ApiError::NotFound(format!("Notebook {} not found", id))
        }
        other => ApiError::Store(other),
    })?;

    // Non-admin users must have read access
    if !user.is_admin() && !store.has_read_access(notebook_id, &user.author_id).await? {
        return Err(ApiError::Forbidden("Access denied".to_string()));
    }

    let limit = params.limit.min(1000).max(1);
    let offset = params.offset.max(0);

    let rows = state
        .store()
        .get_usage_log(
            None,
            params.action.as_deref(),
            Some("notebook"),
            Some(&notebook_id.to_string()),
            limit,
            offset,
        )
        .await?;

    let entries: Vec<UsageLogEntry> = rows
        .iter()
        .map(|row| UsageLogEntry {
            id: row.id,
            user_id: row.user_id,
            author_id: author_id_to_hex(&row.author_id),
            action: row.action.clone(),
            resource_type: row.resource_type.clone(),
            resource_id: row.resource_id.clone(),
            details: row.details.clone(),
            ip_address: row.ip_address.clone(),
            created: row.created,
        })
        .collect();

    Ok(Json(UsageLogResponse { entries }))
}

/// Build usage routes.
pub fn routes() -> Router<AppState> {
    Router::new()
        .route("/api/usage", get(get_usage_log))
        .route("/api/notebooks/{id}/usage", get(get_notebook_usage))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_usage_log_params_defaults() {
        let params: UsageLogParams = serde_urlencoded::from_str("").unwrap();
        assert!(params.user_id.is_none());
        assert!(params.action.is_none());
        assert_eq!(params.limit, 100);
        assert_eq!(params.offset, 0);
    }

    #[test]
    fn test_usage_log_entry_serialize() {
        let entry = UsageLogEntry {
            id: 1,
            user_id: Some(Uuid::nil()),
            author_id: "00".repeat(32),
            action: "create_notebook".to_string(),
            resource_type: Some("notebook".to_string()),
            resource_id: Some(Uuid::nil().to_string()),
            details: None,
            ip_address: None,
            created: chrono::Utc::now(),
        };
        let json = serde_json::to_string(&entry).unwrap();
        assert!(json.contains("create_notebook"));
    }
}
```

## Step 3: Register New Route Modules

### File: `notebook/crates/notebook-server/src/routes/mod.rs`

Update to include the new modules:

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
pub mod usage;
pub mod users;

use axum::Router;

use crate::state::AppState;

/// Build the complete router with all routes.
pub fn build_router(state: AppState) -> Router {
    Router::new()
        .merge(health::routes())
        .merge(auth::routes())
        .merge(users::routes())
        .merge(usage::routes())
        .merge(entries::routes())
        .merge(notebooks::routes())
        .merge(observe::routes())
        .merge(share::routes())
        .merge(events::routes())
        .merge(browse::routes())
        .with_state(state)
}
```

## Step 4: Update Existing Routes — Require Auth

Each existing route handler needs to:
1. Add `user: AuthenticatedUser` parameter
2. Replace `AuthorId::zero()` with `AuthorId::from_bytes(user.author_id)`
3. Replace `let requester_id = [0u8; 32]` with `user.author_id`
4. Add usage logging where appropriate

### File: `notebook/crates/notebook-server/src/routes/notebooks.rs`

**Import changes** — add at top:
```rust
use crate::auth::AuthenticatedUser;
use notebook_store::NewUsageLogEntry;
```

**`list_notebooks` handler** — change signature and replace `AuthorId::zero()`:
```rust
async fn list_notebooks(
    State(state): State<AppState>,
    user: AuthenticatedUser,
) -> ApiResult<Json<ListNotebooksResponse>> {
    let store = state.store();
    let author_id = notebook_core::AuthorId::from_bytes(user.author_id);
    let author_bytes = user.author_id;
    // ... rest stays the same, using author_bytes instead of *author_id.as_bytes()
```

**`create_notebook` handler** — same pattern + log action:
```rust
async fn create_notebook(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Json(request): Json<CreateNotebookRequest>,
) -> ApiResult<(StatusCode, Json<CreateNotebookResponse>)> {
    let store = state.store();
    let author_id = notebook_core::AuthorId::from_bytes(user.author_id);
    let author_bytes = user.author_id;

    // Remove the "ensure author exists" block — authors are created at user registration time

    // ... validation and creation stay the same ...

    // After successful creation, log the action:
    store.log_action(&NewUsageLogEntry {
        user_id: Some(user.user_id),
        author_id: user.author_id,
        action: "create_notebook".to_string(),
        resource_type: Some("notebook".to_string()),
        resource_id: Some(notebook_row.id.to_string()),
        details: None,
        ip_address: None,
    }).await.ok();
```

**`delete_notebook` handler** — same pattern + log action:
```rust
async fn delete_notebook(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(notebook_id): Path<Uuid>,
) -> ApiResult<Json<DeleteNotebookResponse>> {
    let store = state.store();
    let author_bytes = user.author_id;
    // ... check ownership using author_bytes ...

    // After deletion, log:
    store.log_action(&NewUsageLogEntry {
        user_id: Some(user.user_id),
        author_id: user.author_id,
        action: "delete_notebook".to_string(),
        resource_type: Some("notebook".to_string()),
        resource_id: Some(notebook_id.to_string()),
        details: None,
        ip_address: None,
    }).await.ok();
```

### File: `notebook/crates/notebook-server/src/routes/entries.rs`

**Import changes** — add:
```rust
use crate::auth::AuthenticatedUser;
use notebook_store::NewUsageLogEntry;
```

**`create_entry` handler**:
```rust
async fn create_entry(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(notebook_id): Path<Uuid>,
    Json(request): Json<CreateEntryRequest>,
) -> ApiResult<(StatusCode, HeaderMap, Json<CreateEntryResponse>)> {
    // Replace: let author_id = AuthorId::zero();
    let author_id = AuthorId::from_bytes(user.author_id);
    // ... rest stays the same using this author_id ...

    // After storing, log:
    state.store().log_action(&NewUsageLogEntry {
        user_id: Some(user.user_id),
        author_id: user.author_id,
        action: "create_entry".to_string(),
        resource_type: Some("notebook".to_string()),
        resource_id: Some(notebook_id.to_string()),
        details: Some(serde_json::json!({"entry_id": entry_id.to_string()})),
        ip_address: None,
    }).await.ok();
```

**`revise_entry` handler** — same pattern:
```rust
async fn revise_entry(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path((notebook_id, entry_id)): Path<(Uuid, Uuid)>,
    Json(request): Json<ReviseRequest>,
) -> ApiResult<(HeaderMap, Json<ReviseResponse>)> {
    let author_id = AuthorId::from_bytes(user.author_id);
    // ... rest stays the same ...
```

**`get_entry` handler** — add auth but no logging needed for reads:
```rust
async fn get_entry(
    State(state): State<AppState>,
    _user: AuthenticatedUser,  // underscore: auth required but no user-specific behavior
    Path((notebook_id, entry_id)): Path<(Uuid, Uuid)>,
    Query(params): Query<GetEntryParams>,
) -> ApiResult<Json<ReadEntryResponse>> {
```

### File: `notebook/crates/notebook-server/src/routes/share.rs`

**Import changes** — add:
```rust
use crate::auth::AuthenticatedUser;
use notebook_store::NewUsageLogEntry;
```

**`grant_access` handler**:
```rust
async fn grant_access(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(notebook_id): Path<Uuid>,
    Json(request): Json<ShareRequest>,
) -> ApiResult<Json<ShareResponse>> {
    let target_author_id = parse_author_id(&request.author_id)?;
    let requester_id = user.author_id;
    // ... check ownership using requester_id ...

    // After granting, log:
    state.store().log_action(&NewUsageLogEntry {
        user_id: Some(user.user_id),
        author_id: user.author_id,
        action: "grant_access".to_string(),
        resource_type: Some("notebook".to_string()),
        resource_id: Some(notebook_id.to_string()),
        details: Some(serde_json::json!({"target_author": request.author_id})),
        ip_address: None,
    }).await.ok();
```

**`revoke_access` handler** — same pattern with `user.author_id`.

**`list_participants` handler**:
```rust
async fn list_participants(
    State(state): State<AppState>,
    user: AuthenticatedUser,
    Path(notebook_id): Path<Uuid>,
) -> ApiResult<Json<ParticipantsResponse>> {
    // ... verify notebook exists ...
    let requester_id = user.author_id;
    // ... check read access using requester_id ...
```

### File: `notebook/crates/notebook-server/src/routes/observe.rs`

```rust
async fn observe_changes(
    State(state): State<AppState>,
    _user: AuthenticatedUser,  // Auth required, no user-specific behavior
    Path(notebook_id): Path<Uuid>,
    Query(params): Query<ObserveParams>,
) -> ApiResult<Json<ObserveResponse>> {
```

### File: `notebook/crates/notebook-server/src/routes/browse.rs`

```rust
async fn browse_notebook(
    State(state): State<AppState>,
    _user: AuthenticatedUser,  // Auth required
    Path(notebook_id): Path<Uuid>,
    Query(params): Query<BrowseParams>,
) -> ApiResult<Json<BrowseResponse>> {
```

### File: `notebook/crates/notebook-server/src/routes/events.rs`

```rust
async fn subscribe_events(
    State(state): State<AppState>,
    _user: AuthenticatedUser,  // Auth required
    Path(notebook_id): Path<Uuid>,
) -> Result<Sse<impl Stream<Item = Result<Event, Infallible>>>, ApiError> {
```

## Step 5: Admin Bootstrap

### File: `notebook/crates/notebook-server/src/main.rs`

Add the bootstrap function and call it after store connection. Add imports:

```rust
use notebook_core::crypto::KeyPair;
use notebook_core::identity::derive_author_id;
use notebook_store::{NewAuthor, NewUser};
use notebook_server::auth;
```

Add after `let store = Store::connect(store_config).await?;` and before building state:

```rust
    // Bootstrap admin user if needed
    bootstrap_admin(&store, &config).await?;
```

Add the function:

```rust
/// Bootstrap admin user on first run.
///
/// If the users table is empty and ADMIN_USERNAME/ADMIN_PASSWORD are set,
/// creates an admin user with a generated Ed25519 keypair.
async fn bootstrap_admin(
    store: &Store,
    config: &ServerConfig,
) -> Result<(), Box<dyn std::error::Error>> {
    // Only bootstrap if users table is empty
    if store.has_users().await.unwrap_or(true) {
        return Ok(());
    }

    let (username, password) = match (&config.admin_username, &config.admin_password) {
        (Some(u), Some(p)) => (u.clone(), p.clone()),
        _ => {
            tracing::info!("No users exist and ADMIN_USERNAME/ADMIN_PASSWORD not set — skipping admin bootstrap");
            return Ok(());
        }
    };

    tracing::info!("Bootstrapping admin user: {}", username);

    // Generate Ed25519 keypair
    let keypair = KeyPair::generate();
    let public_key = keypair.public_key();
    let author_id = derive_author_id(&public_key);

    // Register author
    let new_author = NewAuthor::new(*author_id.as_bytes(), *public_key.as_bytes());
    store.insert_author(&new_author).await?;

    // Hash password
    let password_hash = auth::hash_password(&password)
        .map_err(|e| format!("Failed to hash admin password: {}", e))?;

    // Create admin user
    let new_user = NewUser {
        username: username.clone(),
        display_name: Some("Administrator".to_string()),
        password_hash,
        author_id: *author_id.as_bytes(),
        role: "admin".to_string(),
    };

    let user_row = store.insert_user(&new_user).await?;

    // Store keypair
    store
        .store_user_key(user_row.id, keypair.secret_key_bytes())
        .await?;

    // Create default quota
    store
        .upsert_user_quota(user_row.id, 100, 10000, 10_485_760, 1_073_741_824)
        .await?;

    tracing::info!(
        user_id = %user_row.id,
        username = %username,
        "Admin user bootstrapped successfully"
    );

    Ok(())
}
```

Note: `notebook-server/Cargo.toml` needs `notebook-core` in its dependencies (it already has it).

## Step 6: Add `notebook-core` Dependency to Server Binary

The `main.rs` binary uses `notebook_core::crypto::KeyPair` and `notebook_core::identity::derive_author_id`. These are already available since `notebook-core` is a dependency of `notebook-server`. Ensure the imports reference the crate correctly:

```rust
use notebook_core::crypto::KeyPair;
use notebook_core::identity::derive_author_id;
```

## Verification

After implementing all changes:

1. `cargo build` — compiles with all new routes and auth requirements
2. `cargo test` — all existing tests pass (note: existing route unit tests that only test serialization should still pass; integration tests that hit live endpoints will need auth)
3. `cargo clippy -- -D warnings`
4. Start server with env vars:
   ```bash
   DATABASE_URL=postgres://notebook:notebook_dev@localhost:5432/notebook \
   JWT_SECRET=dev_secret_at_least_32_chars_long_here \
   ADMIN_USERNAME=admin \
   ADMIN_PASSWORD=adminpass123 \
   cargo run --bin notebook-server
   ```
5. Test login:
   ```bash
   curl -X POST http://localhost:3000/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"adminpass123"}'
   ```
6. Test protected route without auth:
   ```bash
   curl http://localhost:3000/notebooks
   # Should return 401
   ```
7. Test protected route with auth:
   ```bash
   TOKEN=$(curl -s -X POST http://localhost:3000/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"adminpass123"}' | jq -r .token)
   curl -H "Authorization: Bearer $TOKEN" http://localhost:3000/notebooks
   # Should return 200 with notebooks list
   ```
8. Test user creation:
   ```bash
   curl -X POST http://localhost:3000/api/users \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"username":"alice","password":"alicepass123","display_name":"Alice"}'
   ```
9. Test usage log:
   ```bash
   curl -H "Authorization: Bearer $TOKEN" http://localhost:3000/api/usage
   ```

## Files Changed Summary

| File | Action |
|------|--------|
| `notebook/crates/notebook-server/src/routes/users.rs` | **New** |
| `notebook/crates/notebook-server/src/routes/usage.rs` | **New** |
| `notebook/crates/notebook-server/src/routes/mod.rs` | Modified — add users, usage modules |
| `notebook/crates/notebook-server/src/routes/notebooks.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/routes/entries.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/routes/share.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/routes/observe.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/routes/browse.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/routes/events.rs` | Modified — add auth |
| `notebook/crates/notebook-server/src/main.rs` | Modified — add admin bootstrap |
