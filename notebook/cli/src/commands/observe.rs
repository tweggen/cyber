//! OBSERVE command - Watch for changes in a notebook.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, format_timestamp, make_request, output};

/// Arguments for the observe command.
#[derive(Args)]
pub struct ObserveArgs {
    /// Notebook ID to observe
    pub notebook_id: Uuid,

    /// Sequence number to observe changes since (exclusive)
    #[arg(long)]
    pub since: Option<u64>,
}

/// Response from observing a notebook.
#[derive(Debug, Deserialize, Serialize)]
pub struct ObserveResponse {
    pub changes: Vec<ChangeEntry>,
    pub notebook_entropy: f64,
    /// Current sequence (Rust server) or since_sequence (bootstrap)
    #[serde(alias = "current_sequence", alias = "since_sequence")]
    pub current_sequence: Option<u64>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct ChangeEntry {
    pub entry_id: Uuid,
    pub operation: String,
    pub author: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub topic: Option<String>,
    pub integration_cost: IntegrationCost,
    pub causal_position: CausalPositionSummary,
    /// Created timestamp - optional in bootstrap server
    #[serde(skip_serializing_if = "Option::is_none")]
    pub created: Option<DateTime<Utc>>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct IntegrationCost {
    pub entries_revised: u32,
    pub references_broken: u32,
    pub catalog_shift: f64,
    pub orphan: bool,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct CausalPositionSummary {
    pub sequence: u64,
    /// Activity context - optional, may be included in some responses
    #[serde(skip_serializing_if = "Option::is_none")]
    pub activity_context: Option<serde_json::Value>,
}

impl HumanReadable for ObserveResponse {
    fn print_human(&self) {
        println!("{}", "Notebook Changes".green().bold());
        println!("{}", "=".repeat(70));
        println!();

        if let Some(seq) = self.current_sequence {
            println!("  {} {}", "Current Sequence:".cyan(), seq);
        }
        println!(
            "  {} {:.1}",
            "Notebook Entropy:".cyan(),
            self.notebook_entropy
        );
        println!("  {} {}", "Changes Found:".cyan(), self.changes.len());

        if self.changes.is_empty() {
            println!();
            println!("  {}", "(No changes since specified sequence)".dimmed());
            return;
        }

        println!();
        println!("{}", "Recent Changes:".yellow());
        println!("{}", "-".repeat(70));

        for change in &self.changes {
            let op_icon = match change.operation.as_str() {
                "write" => "+".green(),
                "revise" => "~".yellow(),
                _ => "?".white(),
            };

            println!();
            println!(
                "  {} [{}] {} {}",
                op_icon,
                change.causal_position.sequence,
                change.operation.to_uppercase().bold(),
                change.entry_id
            );

            if let Some(topic) = &change.topic {
                println!("    {} {}", "Topic:".dimmed(), topic);
            }

            let author_display = if change.author.len() > 16 {
                &change.author[..16]
            } else {
                &change.author
            };
            println!(
                "    {} {}  {} {:.2}",
                "Author:".dimmed(),
                author_display,
                "Cost:".dimmed(),
                change.integration_cost.catalog_shift
            );

            if let Some(created) = &change.created {
                println!("    {} {}", "Time:".dimmed(), format_timestamp(created));
            }

            if change.integration_cost.orphan {
                println!("    {} {}", "Warning:".red().bold(), "Marked as orphan");
            }
        }
    }
}

/// Execute the observe command.
pub async fn execute(base_url: &str, human: bool, args: ObserveArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let mut url = format!("{}/notebooks/{}/observe", base_url, args.notebook_id);

    if let Some(since) = args.since {
        url = format!("{}?since={}", url, since);
    }

    let response: ObserveResponse = make_request(&client, client.get(&url)).await?;

    output(&response, human)
}
