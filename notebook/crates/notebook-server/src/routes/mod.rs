//! Route definitions for the HTTP API.

pub mod authors;
pub mod browse;
pub mod entries;
pub mod events;
pub mod health;
pub mod notebooks;
pub mod observe;
pub mod share;

use axum::Router;

use crate::state::AppState;

/// Build the complete router with all routes.
pub fn build_router(state: AppState) -> Router {
    Router::new()
        .merge(health::routes())
        .merge(authors::routes())
        .merge(entries::routes())
        .merge(notebooks::routes())
        .merge(observe::routes())
        .merge(share::routes())
        .merge(events::routes())
        .merge(browse::routes())
        .with_state(state)
}
