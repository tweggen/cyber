//! Entry point for the notebook-server binary.

use std::net::SocketAddr;

use axum::middleware;
use notebook_server::{
    config::ServerConfig,
    middleware::request_id::{propagate_request_id, request_id_layer},
    routes,
    state::AppState,
};
use notebook_store::{Store, StoreConfig};
use tokio::net::TcpListener;
use tokio::signal;
use tower_http::cors::{Any, CorsLayer};
use tower_http::trace::TraceLayer;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt, EnvFilter};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Load configuration
    let config = ServerConfig::from_env()?;

    // Initialize tracing
    init_tracing(&config.log_level);

    tracing::info!("Starting notebook-server");
    tracing::info!("Configuration: port={}, log_level={}", config.port, config.log_level);

    // Connect to database
    let store_config = StoreConfig::from_env()?;
    let store = Store::connect(store_config).await?;
    tracing::info!("Connected to database");

    // Build application state
    let state = AppState::new(store, config.clone());

    // Build CORS layer
    let cors = build_cors_layer(&config.cors_allowed_origins);

    // Build router with middleware
    let app = routes::build_router(state)
        .layer(middleware::from_fn(propagate_request_id))
        .layer(request_id_layer())
        .layer(cors)
        .layer(TraceLayer::new_for_http());

    // Create listener
    let addr = config.socket_addr();
    let listener = TcpListener::bind(addr).await?;
    tracing::info!("Listening on {}", addr);

    // Run server with graceful shutdown
    axum::serve(listener, app)
        .with_graceful_shutdown(shutdown_signal())
        .await?;

    tracing::info!("Server shutdown complete");
    Ok(())
}

/// Initialize the tracing subscriber.
fn init_tracing(log_level: &str) {
    let filter = EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| EnvFilter::new(log_level));

    tracing_subscriber::registry()
        .with(filter)
        .with(tracing_subscriber::fmt::layer())
        .init();
}

/// Build CORS layer from configuration.
fn build_cors_layer(allowed_origins: &str) -> CorsLayer {
    if allowed_origins == "*" {
        CorsLayer::new()
            .allow_origin(Any)
            .allow_methods(Any)
            .allow_headers(Any)
    } else {
        // Parse comma-separated origins
        let origins: Vec<_> = allowed_origins
            .split(',')
            .map(|s| s.trim().parse().expect("Invalid CORS origin"))
            .collect();

        CorsLayer::new()
            .allow_origin(origins)
            .allow_methods(Any)
            .allow_headers(Any)
    }
}

/// Wait for shutdown signal (Ctrl+C or SIGTERM).
async fn shutdown_signal() {
    let ctrl_c = async {
        signal::ctrl_c()
            .await
            .expect("Failed to install Ctrl+C handler");
    };

    #[cfg(unix)]
    let terminate = async {
        signal::unix::signal(signal::unix::SignalKind::terminate())
            .expect("Failed to install SIGTERM handler")
            .recv()
            .await;
    };

    #[cfg(not(unix))]
    let terminate = std::future::pending::<()>();

    tokio::select! {
        _ = ctrl_c => {
            tracing::info!("Received Ctrl+C, starting graceful shutdown");
        }
        _ = terminate => {
            tracing::info!("Received SIGTERM, starting graceful shutdown");
        }
    }
}
