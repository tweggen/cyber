//! Command implementations for the notebook CLI.
//!
//! Each command module provides:
//! - Args struct for clap argument parsing
//! - execute() function that performs the command
//! - Human-readable and JSON output formatting

pub mod browse;
pub mod create;
pub mod delete;
pub mod list;
pub mod observe;
pub mod read;
pub mod rename;
pub mod revise;
pub mod share;
pub mod write;

use anyhow::Result;
use reqwest::header::{HeaderMap, HeaderValue, AUTHORIZATION};
use serde::Serialize;

/// Common error type for HTTP requests.
#[derive(Debug, thiserror::Error)]
pub enum CliError {
    #[error("HTTP request failed: {0}")]
    Http(#[from] reqwest::Error),

    #[error("Server error ({status}): {message}")]
    Server { status: u16, message: String },
}

/// Build an HTTP client, optionally configured with a Bearer token.
pub fn build_client(token: Option<&str>) -> Result<reqwest::Client> {
    let mut builder = reqwest::Client::builder();

    if let Some(token) = token {
        let mut headers = HeaderMap::new();
        let value = HeaderValue::from_str(&format!("Bearer {}", token))
            .map_err(|e| anyhow::anyhow!("Invalid token value: {}", e))?;
        headers.insert(AUTHORIZATION, value);
        builder = builder.default_headers(headers);
    }

    Ok(builder.build()?)
}

/// Print output in JSON or human-readable format.
pub fn output<T: Serialize + HumanReadable>(value: &T, human: bool) -> Result<()> {
    if human {
        value.print_human();
    } else {
        println!("{}", serde_json::to_string_pretty(value)?);
    }
    Ok(())
}

/// Trait for types that can be printed in human-readable format.
pub trait HumanReadable {
    fn print_human(&self);
}

/// Make an HTTP request and handle common error cases.
pub async fn make_request<T: serde::de::DeserializeOwned>(
    _client: &reqwest::Client,
    request: reqwest::RequestBuilder,
) -> Result<T, CliError> {
    let response = request.send().await?;
    let status = response.status();

    if status.is_success() {
        let body = response.json::<T>().await?;
        Ok(body)
    } else {
        let body = response.text().await.unwrap_or_default();

        // Try to parse as JSON error
        if let Ok(json) = serde_json::from_str::<serde_json::Value>(&body) {
            let message = json
                .get("error")
                .and_then(|v| v.as_str())
                .unwrap_or(&body)
                .to_string();
            Err(CliError::Server {
                status: status.as_u16(),
                message,
            })
        } else {
            Err(CliError::Server {
                status: status.as_u16(),
                message: body,
            })
        }
    }
}

/// Format a timestamp for human display.
pub fn format_timestamp(ts: &chrono::DateTime<chrono::Utc>) -> String {
    ts.format("%Y-%m-%d %H:%M:%S UTC").to_string()
}

/// Truncate a string for display, adding ellipsis if needed.
pub fn truncate(s: &str, max_len: usize) -> String {
    if s.len() <= max_len {
        s.to_string()
    } else {
        format!("{}...", &s[..max_len.saturating_sub(3)])
    }
}
