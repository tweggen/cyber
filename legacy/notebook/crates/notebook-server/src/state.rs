//! Application state shared across handlers.

use std::sync::Arc;

use notebook_entropy::IntegrationCostEngine;
use notebook_store::Store;
use tokio::sync::Mutex;

use crate::config::ServerConfig;
use crate::events::EventBroadcaster;

/// Application state shared across all handlers.
///
/// This is cloneable and can be extracted in handlers using `State<AppState>`.
#[derive(Clone)]
pub struct AppState {
    /// Database store.
    store: Arc<Store>,
    /// Server configuration.
    config: Arc<ServerConfig>,
    /// Integration cost engine for entropy computation.
    engine: Arc<Mutex<IntegrationCostEngine>>,
    /// Event broadcaster for SSE notifications.
    broadcaster: Arc<EventBroadcaster>,
}

impl AppState {
    /// Create new application state.
    pub fn new(store: Store, config: ServerConfig) -> Self {
        Self {
            store: Arc::new(store),
            config: Arc::new(config),
            engine: Arc::new(Mutex::new(IntegrationCostEngine::new())),
            broadcaster: Arc::new(EventBroadcaster::new()),
        }
    }

    /// Get a reference to the database store.
    pub fn store(&self) -> &Store {
        &self.store
    }

    /// Get a reference to the server configuration.
    pub fn config(&self) -> &ServerConfig {
        &self.config
    }

    /// Get a reference to the integration cost engine.
    pub fn engine(&self) -> &Arc<Mutex<IntegrationCostEngine>> {
        &self.engine
    }

    /// Get a reference to the event broadcaster.
    pub fn broadcaster(&self) -> &Arc<EventBroadcaster> {
        &self.broadcaster
    }
}

impl std::fmt::Debug for AppState {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("AppState")
            .field("config", &self.config)
            .finish_non_exhaustive()
    }
}
