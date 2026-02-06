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

        Ok(Self {
            database_url,
            port,
            log_level,
            cors_allowed_origins,
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

        // SAFETY: This test is not run in parallel with other tests that read DATABASE_URL.
        unsafe { env::remove_var("DATABASE_URL") };
    }
}
