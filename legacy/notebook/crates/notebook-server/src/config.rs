//! Server configuration from environment variables.

use std::env;

/// Server configuration.
#[derive(Debug, Clone)]
pub struct ServerConfig {
    /// Database connection URL.
    pub database_url: String,
    /// Server port to listen on.
    pub port: u16,
    /// Log level (trace, debug, info, warn, error).
    pub log_level: String,
    /// CORS allowed origins (comma-separated or "*" for all).
    pub cors_allowed_origins: String,
    /// Ed25519 public key in PEM format for JWT validation.
    /// If empty, JWT validation is disabled (dev mode only).
    pub jwt_public_key: String,
    /// Allow X-Author-Id header fallback for dev mode.
    /// When true, requests without a JWT Bearer token can use X-Author-Id.
    pub allow_dev_identity: bool,
    /// Whether to enforce JWT scope claims on endpoints.
    /// When true, endpoints require matching scope (e.g. `notebook:read`).
    /// When false, any valid JWT grants full access (backward-compatible).
    pub enforce_scopes: bool,
}

impl ServerConfig {
    /// Load configuration from environment variables.
    ///
    /// Required:
    /// - `DATABASE_URL`: Database connection string
    ///
    /// Optional:
    /// - `PORT`: Server port (default: 3000)
    /// - `LOG_LEVEL`: Logging level (default: "info")
    /// - `CORS_ALLOWED_ORIGINS`: Allowed CORS origins (default: "*")
    pub fn from_env() -> Result<Self, ConfigError> {
        let database_url = env::var("DATABASE_URL")
            .map_err(|_| ConfigError::MissingEnvVar("DATABASE_URL".to_string()))?;

        let port = env::var("PORT")
            .ok()
            .and_then(|s| s.parse().ok())
            .unwrap_or(3000);

        let log_level = env::var("LOG_LEVEL").unwrap_or_else(|_| "info".to_string());

        let cors_allowed_origins =
            env::var("CORS_ALLOWED_ORIGINS").unwrap_or_else(|_| "*".to_string());

        let jwt_public_key = env::var("JWT_PUBLIC_KEY").unwrap_or_default();

        let allow_dev_identity = env::var("ALLOW_DEV_IDENTITY")
            .map(|v| v == "true" || v == "1")
            .unwrap_or(false);

        let enforce_scopes = env::var("ENFORCE_SCOPES")
            .map(|v| v == "true" || v == "1")
            .unwrap_or(true);

        Ok(Self {
            database_url,
            port,
            log_level,
            cors_allowed_origins,
            jwt_public_key,
            allow_dev_identity,
            enforce_scopes,
        })
    }

    /// Get the socket address for the server.
    pub fn socket_addr(&self) -> std::net::SocketAddr {
        std::net::SocketAddr::from(([0, 0, 0, 0], self.port))
    }
}

/// Configuration errors.
#[derive(Debug, thiserror::Error)]
pub enum ConfigError {
    /// Required environment variable is missing.
    #[error("missing required environment variable: {0}")]
    MissingEnvVar(String),

    /// Invalid environment variable value.
    #[error("invalid value for environment variable {name}: {reason}")]
    InvalidValue { name: String, reason: String },
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_values() {
        // This test requires DATABASE_URL to be set
        // SAFETY: This test is not run in parallel with other tests that read DATABASE_URL.
        unsafe { env::set_var("DATABASE_URL", "postgres://test:test@localhost/test") };

        let config = ServerConfig::from_env().unwrap();

        assert_eq!(config.port, 3000);
        assert_eq!(config.log_level, "info");
        assert_eq!(config.cors_allowed_origins, "*");
        assert!(config.jwt_public_key.is_empty());
        assert!(!config.allow_dev_identity);
        assert!(config.enforce_scopes);

        // SAFETY: This test is not run in parallel with other tests that read DATABASE_URL.
        unsafe { env::remove_var("DATABASE_URL") };
    }
}
