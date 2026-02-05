//! notebook-server: HTTP API server for the Knowledge Exchange Platform
//!
//! This crate provides:
//! - REST API endpoints (READ, WRITE, BROWSE, REVISE)
//! - Authentication and session management
//! - Rate limiting and request validation
//! - Server-Sent Events (SSE) for real-time notifications
//!
//! # Architecture
//!
//! The server is built on Axum with a middleware stack for:
//! - Request tracing and logging
//! - CORS handling
//! - Request ID generation
//! - JSON error responses
//!
//! # Usage
//!
//! ```rust,ignore
//! use notebook_server::{config::ServerConfig, run_server};
//!
//! #[tokio::main]
//! async fn main() -> Result<(), Box<dyn std::error::Error>> {
//!     let config = ServerConfig::from_env()?;
//!     run_server(config).await?;
//!     Ok(())
//! }
//! ```
//!
//! Owned by: agent-server

pub mod config;
pub mod error;
pub mod events;
pub mod middleware;
pub mod routes;
pub mod state;

// Re-exports for convenience
pub use config::{ConfigError, ServerConfig};
pub use error::{ApiError, ApiResult};
pub use events::EventBroadcaster;
pub use state::AppState;

// Re-export dependent crates
pub use notebook_core;
pub use notebook_store;
