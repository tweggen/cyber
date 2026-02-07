//! Entry point for the notebook-server binary.

use axum::middleware;
use notebook_server::{
    auth,
    config::ServerConfig,
    middleware::request_id::{propagate_request_id, request_id_layer},
    routes,
    state::AppState,
};
use notebook_store::{NewUser, Store, StoreConfig};
use tokio::net::TcpListener;
use tokio::signal;
use tower_http::cors::{Any, CorsLayer};
use tower_http::trace::TraceLayer;
use tracing_subscriber::{EnvFilter, layer::SubscriberExt, util::SubscriberInitExt};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Load configuration
    let config = ServerConfig::from_env()?;

    // Initialize tracing
    init_tracing(&config.log_level);

    tracing::info!("Starting notebook-server");
    tracing::info!(
        "Configuration: port={}, log_level={}",
        config.port,
        config.log_level
    );

    // Connect to database
    let store_config = StoreConfig::from_env()?;
    let store = Store::connect(store_config).await?;
    tracing::info!("Connected to database");

    // Bootstrap admin user if configured and no users exist yet
    bootstrap_admin(&store, &config).await?;

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

/// Bootstrap the initial admin user if ADMIN_USERNAME and ADMIN_PASSWORD are set
/// and no users exist in the database yet.
async fn bootstrap_admin(
    store: &Store,
    config: &ServerConfig,
) -> Result<(), Box<dyn std::error::Error>> {
    let (Some(admin_username), Some(admin_password)) =
        (&config.admin_username, &config.admin_password)
    else {
        return Ok(());
    };

    // Only bootstrap if no users exist
    let has_users = store.has_users().await?;
    if has_users {
        tracing::debug!("Users already exist, skipping admin bootstrap");
        return Ok(());
    }

    tracing::info!(
        "No users found, bootstrapping admin user '{}'",
        admin_username
    );

    // Generate Ed25519 keypair for the admin user
    use ed25519_dalek::SigningKey;
    use rand::rngs::OsRng;

    let signing_key = SigningKey::generate(&mut OsRng);
    let public_key = signing_key.verifying_key();
    let public_key_bytes = public_key.to_bytes();

    // Register the author (computes AuthorId as BLAKE3 hash of public key)
    let author_id_bytes = blake3::hash(&public_key_bytes);
    let author_id_arr: [u8; 32] = *author_id_bytes.as_bytes();

    let new_author = notebook_store::NewAuthor::new(author_id_arr, public_key_bytes);
    store.insert_author(&new_author).await?;

    // Hash the admin password
    let password_hash = auth::hash_password(admin_password)
        .map_err(|e| format!("Failed to hash admin password: {}", e))?;

    // Create the admin user
    let new_user = NewUser {
        username: admin_username.clone(),
        display_name: Some("Administrator".to_string()),
        password_hash,
        author_id: author_id_arr,
        role: "admin".to_string(),
    };
    let user = store.insert_user(&new_user).await?;

    // Store the signing key (unencrypted for now — production should use a KMS)
    store
        .store_user_key(user.id, &signing_key.to_bytes())
        .await?;

    // Create default quotas for admin (generous defaults)
    store
        .upsert_user_quota(user.id, 100, 10000, 10_485_760, 1_073_741_824)
        .await?;

    let author_id_hex: String = author_id_arr.iter().map(|b| format!("{:02x}", b)).collect();
    tracing::info!(
        user_id = %user.id,
        username = %user.username,
        author_id = %author_id_hex,
        "Admin user created successfully"
    );

    Ok(())
}

/// Initialize the tracing subscriber.
fn init_tracing(log_level: &str) {
    let filter = EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new(log_level));

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
