//! Request ID middleware for tracing requests.

use axum::{
    extract::Request,
    middleware::Next,
    response::Response,
};
use http::HeaderValue;
use tower_http::request_id::{MakeRequestId, RequestId};
use uuid::Uuid;

/// Header name for request ID.
pub const REQUEST_ID_HEADER: &str = "x-request-id";

/// Generate UUID-based request IDs.
#[derive(Clone, Copy, Debug, Default)]
pub struct MakeRequestUuid;

impl MakeRequestId for MakeRequestUuid {
    fn make_request_id<B>(&mut self, _request: &http::Request<B>) -> Option<RequestId> {
        let id = Uuid::new_v4().to_string();
        HeaderValue::from_str(&id)
            .ok()
            .map(RequestId::new)
    }
}

/// Tower layer for request ID generation.
pub type RequestIdLayer = tower_http::request_id::SetRequestIdLayer<MakeRequestUuid>;

/// Create a new request ID layer.
pub fn request_id_layer() -> RequestIdLayer {
    tower_http::request_id::SetRequestIdLayer::new(
        REQUEST_ID_HEADER.parse().unwrap(),
        MakeRequestUuid,
    )
}

/// Middleware that propagates request ID to response headers.
pub async fn propagate_request_id(request: Request, next: Next) -> Response {
    let request_id = request
        .headers()
        .get(REQUEST_ID_HEADER)
        .cloned();

    let mut response = next.run(request).await;

    if let Some(id) = request_id {
        response.headers_mut().insert(REQUEST_ID_HEADER, id);
    }

    response
}
