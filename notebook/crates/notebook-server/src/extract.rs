//! Author identity extraction from JWT Bearer token or X-Author-Id header (dev mode).

use axum::{extract::FromRequestParts, http::request::Parts};
use jsonwebtoken::{Algorithm, DecodingKey, TokenData, Validation};
use notebook_core::AuthorId;
use serde::Deserialize;

use crate::error::ApiError;
use crate::state::AppState;

/// JWT claims structure.
#[derive(Debug, Deserialize)]
pub struct Claims {
    /// Subject — the AuthorId as 64-char hex string.
    pub sub: String,
    /// Issuer.
    #[serde(default)]
    pub iss: Option<String>,
    /// Scope (space-separated permissions, for future use).
    #[serde(default)]
    pub scope: Option<String>,
}

/// Extracts AuthorId and scopes from a JWT Bearer token or X-Author-Id header (dev fallback).
///
/// Priority:
/// 1. `Authorization: Bearer <jwt>` — validates signature, extracts `sub` claim as AuthorId
///    and `scope` claim as a list of scopes.
/// 2. `X-Author-Id` header — only if `allow_dev_identity` is true in config.
///    Grants all scopes (read, write, share, admin).
/// 3. If neither is present and `allow_dev_identity` is true, returns `AuthorId::zero()`
///    with all scopes.
/// 4. Otherwise returns `Unauthorized`.
pub struct AuthorIdentity {
    pub author_id: AuthorId,
    pub scopes: Vec<String>,
}

/// All available scopes for dev/admin use.
const ALL_SCOPES: &[&str] = &[
    "notebook:read",
    "notebook:write",
    "notebook:share",
    "notebook:admin",
];

/// Check that `identity` has the required `scope`.
///
/// If `config.enforce_scopes` is false, this always succeeds.
/// Otherwise, returns `Forbidden` if the scope is missing.
pub fn require_scope(
    identity: &AuthorIdentity,
    scope: &str,
    config: &crate::config::ServerConfig,
) -> Result<(), ApiError> {
    if !config.enforce_scopes {
        return Ok(());
    }
    if identity.scopes.iter().any(|s| s == scope) {
        Ok(())
    } else {
        Err(ApiError::Forbidden(format!(
            "Missing required scope: {}",
            scope
        )))
    }
}

impl FromRequestParts<AppState> for AuthorIdentity {
    type Rejection = ApiError;

    async fn from_request_parts(
        parts: &mut Parts,
        state: &AppState,
    ) -> Result<Self, Self::Rejection> {
        let config = state.config();

        // Try JWT Bearer token first
        if let Some(auth_header) = parts.headers.get("Authorization") {
            let auth_str = auth_header.to_str().map_err(|_| {
                ApiError::Unauthorized("Authorization header contains invalid characters".into())
            })?;

            if let Some(token) = auth_str.strip_prefix("Bearer ") {
                return extract_from_jwt(token.trim(), config);
            }
        }

        // Fall back to X-Author-Id header (dev mode only)
        if config.allow_dev_identity {
            return extract_from_dev_header(parts);
        }

        Err(ApiError::Unauthorized(
            "Missing Authorization: Bearer <jwt> header".into(),
        ))
    }
}

/// Validate JWT and extract AuthorId + scopes from claims.
fn extract_from_jwt(
    token: &str,
    config: &crate::config::ServerConfig,
) -> Result<AuthorIdentity, ApiError> {
    if config.jwt_public_key.is_empty() {
        return Err(ApiError::Internal(
            "JWT_PUBLIC_KEY not configured on server".into(),
        ));
    }

    let key = DecodingKey::from_ed_pem(config.jwt_public_key.as_bytes()).map_err(|e| {
        tracing::error!(error = %e, "Failed to parse JWT public key");
        ApiError::Internal("Invalid JWT public key configuration".into())
    })?;

    let mut validation = Validation::new(Algorithm::EdDSA);
    validation.set_issuer(&["notebook-admin"]);
    validation.validate_exp = true;
    validation.validate_nbf = true;

    let token_data: TokenData<Claims> =
        jsonwebtoken::decode(token, &key, &validation).map_err(|e| {
            tracing::debug!(error = %e, "JWT validation failed");
            ApiError::Unauthorized(format!("Invalid token: {}", e))
        })?;

    let author_id = parse_author_id_hex(&token_data.claims.sub)?;

    // Parse space-separated scope string into Vec<String>
    let scopes = token_data
        .claims
        .scope
        .unwrap_or_default()
        .split_whitespace()
        .map(String::from)
        .collect();

    Ok(AuthorIdentity { author_id, scopes })
}

/// Extract AuthorId from the X-Author-Id header (dev mode fallback).
/// Dev mode grants all scopes.
fn extract_from_dev_header(parts: &Parts) -> Result<AuthorIdentity, ApiError> {
    let all_scopes: Vec<String> = ALL_SCOPES.iter().map(|s| (*s).to_string()).collect();

    let Some(header_value) = parts.headers.get("X-Author-Id") else {
        tracing::warn!("No auth provided, using zero author (dev mode)");
        return Ok(AuthorIdentity {
            author_id: AuthorId::zero(),
            scopes: all_scopes,
        });
    };

    let hex_str = header_value.to_str().map_err(|_| {
        ApiError::BadRequest("X-Author-Id header contains invalid characters".to_string())
    })?;

    let author_id = parse_author_id_hex(hex_str)?;
    tracing::debug!(author_id = %hex_str, "Using dev identity from X-Author-Id header");
    Ok(AuthorIdentity {
        author_id,
        scopes: all_scopes,
    })
}

/// Parse a 64-char hex string into an AuthorId.
fn parse_author_id_hex(hex_str: &str) -> Result<AuthorId, ApiError> {
    if hex_str.len() != 64 {
        return Err(ApiError::BadRequest(format!(
            "AuthorId must be 64 hex characters, got {}",
            hex_str.len()
        )));
    }

    let bytes = hex::decode(hex_str)
        .map_err(|e| ApiError::BadRequest(format!("Invalid hex in AuthorId: {}", e)))?;

    let mut arr = [0u8; 32];
    arr.copy_from_slice(&bytes);
    Ok(AuthorId::from_bytes(arr))
}

#[cfg(test)]
mod tests {
    use super::*;
    use jsonwebtoken::EncodingKey;

    // Dev key pair for testing (Ed25519, generated with openssl genpkey -algorithm Ed25519)
    const TEST_PRIVATE_KEY_PEM: &str = "-----BEGIN PRIVATE KEY-----\n\
        MC4CAQAwBQYDK2VwBCIEIIYgecUAnMtQL6ICji1OF4vFg4AyoRPmI/JOtyWC4TZY\n\
        -----END PRIVATE KEY-----";

    const TEST_PUBLIC_KEY_PEM: &str = "-----BEGIN PUBLIC KEY-----\n\
        MCowBQYDK2VwAyEAF77yKVNJ+mfeSoEm43HP2z+/upKP2Od7DYjiWhJxNjA=\n\
        -----END PUBLIC KEY-----";

    fn test_config(public_key: &str, allow_dev: bool) -> crate::config::ServerConfig {
        crate::config::ServerConfig {
            database_url: String::new(),
            port: 3000,
            log_level: "info".into(),
            cors_allowed_origins: "*".into(),
            jwt_public_key: public_key.to_string(),
            allow_dev_identity: allow_dev,
            enforce_scopes: true,
        }
    }

    fn create_test_token(author_id_hex: &str) -> String {
        let key = EncodingKey::from_ed_pem(TEST_PRIVATE_KEY_PEM.as_bytes()).unwrap();
        let now = chrono::Utc::now().timestamp() as usize;
        let claims = serde_json::json!({
            "sub": author_id_hex,
            "iss": "notebook-admin",
            "exp": now + 3600,
            "nbf": now - 10,
            "iat": now,
            "scope": "notebook:read notebook:write",
        });
        let header = jsonwebtoken::Header::new(Algorithm::EdDSA);
        jsonwebtoken::encode(&header, &claims, &key).unwrap()
    }

    #[test]
    fn test_parse_author_id_hex_valid() {
        let hex = "a".repeat(64);
        let result = parse_author_id_hex(&hex);
        assert!(result.is_ok());
    }

    #[test]
    fn test_parse_author_id_hex_wrong_length() {
        let hex = "a".repeat(32);
        let result = parse_author_id_hex(&hex);
        assert!(result.is_err());
    }

    #[test]
    fn test_parse_author_id_hex_invalid_hex() {
        let hex = "g".repeat(64);
        let result = parse_author_id_hex(&hex);
        assert!(result.is_err());
    }

    #[test]
    fn test_extract_from_jwt_no_key_configured() {
        let config = test_config("", false);
        let result = extract_from_jwt("some.token.here", &config);
        assert!(result.is_err());
    }

    #[test]
    fn test_extract_from_jwt_valid_token() {
        let author_hex = "a".repeat(64);
        let token = create_test_token(&author_hex);
        let config = test_config(TEST_PUBLIC_KEY_PEM, false);
        let result = extract_from_jwt(&token, &config);
        assert!(result.is_ok());
        let identity = result.unwrap();
        assert_eq!(hex::encode(identity.author_id.as_bytes()), author_hex);
        assert!(identity.scopes.contains(&"notebook:read".to_string()));
        assert!(identity.scopes.contains(&"notebook:write".to_string()));
    }

    #[test]
    fn test_extract_from_jwt_wrong_key_rejected() {
        // Generate a token with the test private key
        let author_hex = "b".repeat(64);
        let token = create_test_token(&author_hex);

        // Try to validate with a different public key
        let wrong_public_key = "-----BEGIN PUBLIC KEY-----\n\
            MCowBQYDK2VwAyEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n\
            -----END PUBLIC KEY-----";
        let config = test_config(wrong_public_key, false);
        let result = extract_from_jwt(&token, &config);
        assert!(result.is_err());
    }

    #[test]
    fn test_extract_from_jwt_expired_token() {
        let key = EncodingKey::from_ed_pem(TEST_PRIVATE_KEY_PEM.as_bytes()).unwrap();
        let author_hex = "c".repeat(64);
        let past = chrono::Utc::now().timestamp() as usize - 7200; // 2 hours ago
        let claims = serde_json::json!({
            "sub": author_hex,
            "iss": "notebook-admin",
            "exp": past + 3600, // expired 1 hour ago
            "nbf": past,
        });
        let header = jsonwebtoken::Header::new(Algorithm::EdDSA);
        let token = jsonwebtoken::encode(&header, &claims, &key).unwrap();

        let config = test_config(TEST_PUBLIC_KEY_PEM, false);
        let result = extract_from_jwt(&token, &config);
        assert!(result.is_err());
    }

    #[test]
    fn test_require_scope_enforced_present() {
        let identity = AuthorIdentity {
            author_id: AuthorId::zero(),
            scopes: vec!["notebook:read".to_string(), "notebook:write".to_string()],
        };
        let config = test_config("", false); // enforce_scopes = true
        assert!(require_scope(&identity, "notebook:read", &config).is_ok());
        assert!(require_scope(&identity, "notebook:write", &config).is_ok());
    }

    #[test]
    fn test_require_scope_enforced_missing() {
        let identity = AuthorIdentity {
            author_id: AuthorId::zero(),
            scopes: vec!["notebook:read".to_string()],
        };
        let config = test_config("", false);
        assert!(require_scope(&identity, "notebook:write", &config).is_err());
        assert!(require_scope(&identity, "notebook:admin", &config).is_err());
    }

    #[test]
    fn test_require_scope_not_enforced() {
        let identity = AuthorIdentity {
            author_id: AuthorId::zero(),
            scopes: vec![], // no scopes at all
        };
        let mut config = test_config("", false);
        config.enforce_scopes = false;
        // Should pass even with no scopes when enforcement is off
        assert!(require_scope(&identity, "notebook:admin", &config).is_ok());
    }

    #[test]
    fn test_extract_from_jwt_wrong_issuer() {
        let key = EncodingKey::from_ed_pem(TEST_PRIVATE_KEY_PEM.as_bytes()).unwrap();
        let author_hex = "d".repeat(64);
        let now = chrono::Utc::now().timestamp() as usize;
        let claims = serde_json::json!({
            "sub": author_hex,
            "iss": "wrong-issuer",
            "exp": now + 3600,
            "nbf": now - 10,
        });
        let header = jsonwebtoken::Header::new(Algorithm::EdDSA);
        let token = jsonwebtoken::encode(&header, &claims, &key).unwrap();

        let config = test_config(TEST_PUBLIC_KEY_PEM, false);
        let result = extract_from_jwt(&token, &config);
        assert!(result.is_err());
    }
}
