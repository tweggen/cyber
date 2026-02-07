//! Authentication routes: login, logout, me, change-password.

use axum::{
    Json, Router,
    extract::State,
    http::StatusCode,
    routing::{get, post},
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

    let user = store
        .get_user_by_username(&request.username)
        .await?
        .ok_or_else(|| ApiError::Unauthorized("Invalid username or password".to_string()))?;

    if !user.is_active {
        return Err(ApiError::Unauthorized("Account is deactivated".to_string()));
    }

    let valid = auth::verify_password(&request.password, &user.password_hash)?;
    if !valid {
        return Err(ApiError::Unauthorized(
            "Invalid username or password".to_string(),
        ));
    }

    let author_id_hex: String = user
        .author_id
        .iter()
        .map(|b| format!("{:02x}", b))
        .collect();

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
async fn me(State(state): State<AppState>, user: AuthenticatedUser) -> ApiResult<Json<MeResponse>> {
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

    let user_row = store.get_user_by_id(user.user_id).await?;

    let valid = auth::verify_password(&request.current_password, &user_row.password_hash)?;
    if !valid {
        return Err(ApiError::Unauthorized(
            "Current password is incorrect".to_string(),
        ));
    }

    if request.new_password.len() < 8 {
        return Err(ApiError::BadRequest(
            "New password must be at least 8 characters".to_string(),
        ));
    }

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
