//! Authentication module: JWT token management and password hashing.

use argon2::{
    Argon2,
    password_hash::{PasswordHash, PasswordHasher, PasswordVerifier, SaltString, rand_core::OsRng},
};
use axum::{
    extract::FromRequestParts,
    http::{header, request::Parts},
};
use jsonwebtoken::{DecodingKey, EncodingKey, Header, Validation, decode, encode};
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

        let token = auth_header.strip_prefix("Bearer ").ok_or_else(|| {
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
