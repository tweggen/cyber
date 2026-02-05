//! READ command - Retrieve an entry with its metadata.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{format_timestamp, make_request, output, truncate, HumanReadable};

/// Arguments for the read command.
#[derive(Args)]
pub struct ReadArgs {
    /// Notebook ID containing the entry
    pub notebook_id: Uuid,

    /// Entry ID to read
    pub entry_id: Uuid,

    /// Specific revision number to retrieve (0 = current)
    #[arg(short = 'r', long)]
    pub revision: Option<u32>,
}

/// Response from reading an entry.
#[derive(Debug, Deserialize, Serialize)]
pub struct ReadEntryResponse {
    pub entry: EntryResponse,
    pub revisions: Vec<EntrySummary>,
    #[serde(default)]
    pub references: Vec<EntrySummary>,
    #[serde(default)]
    pub referenced_by: Vec<EntrySummary>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct EntryResponse {
    pub id: Uuid,
    pub content: serde_json::Value, // Can be string or {data, encoding} for binary
    pub content_type: String,
    pub topic: Option<String>,
    pub author: String,
    pub references: Vec<Uuid>,
    pub revision_of: Option<Uuid>,
    pub causal_position: CausalPositionResponse,
    pub created: DateTime<Utc>,
    pub integration_cost: IntegrationCost,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct CausalPositionResponse {
    pub sequence: u64,
    pub activity_context: ActivityContext,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct ActivityContext {
    pub entries_since_last_by_author: u32,
    pub total_notebook_entries: u32,
    pub recent_entropy: f64,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct IntegrationCost {
    pub entries_revised: u32,
    pub references_broken: u32,
    pub catalog_shift: f64,
    pub orphan: bool,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct EntrySummary {
    pub id: Uuid,
    pub topic: Option<String>,
    pub author: String,
    pub created: DateTime<Utc>,
}

impl HumanReadable for ReadEntryResponse {
    fn print_human(&self) {
        let entry = &self.entry;

        println!("{}", "Entry Details".green().bold());
        println!("{}", "=".repeat(60));
        println!();

        println!("  {} {}", "ID:".cyan(), entry.id);
        println!("  {} {}", "Content Type:".cyan(), entry.content_type);
        if let Some(topic) = &entry.topic {
            println!("  {} {}", "Topic:".cyan(), topic);
        }
        println!(
            "  {} {}",
            "Author:".cyan(),
            truncate(&entry.author, 16)
        );
        println!(
            "  {} {}",
            "Created:".cyan(),
            format_timestamp(&entry.created)
        );
        println!(
            "  {} {}",
            "Sequence:".cyan(),
            entry.causal_position.sequence
        );

        if let Some(rev_of) = &entry.revision_of {
            println!("  {} {}", "Revision Of:".cyan(), rev_of);
        }

        println!();
        println!("{}", "Content:".yellow());
        println!("{}", "-".repeat(60));

        // Handle content (string or binary)
        match &entry.content {
            serde_json::Value::String(s) => {
                println!("{}", s);
            }
            serde_json::Value::Object(obj) => {
                if let Some(encoding) = obj.get("encoding") {
                    println!(
                        "[Binary content, {} encoded, {} bytes]",
                        encoding,
                        obj.get("data")
                            .and_then(|d| d.as_str())
                            .map(|s| s.len())
                            .unwrap_or(0)
                    );
                }
            }
            _ => {
                println!("{}", entry.content);
            }
        }

        println!("{}", "-".repeat(60));
        println!();

        // Integration cost
        println!("{}", "Integration Cost:".yellow());
        println!(
            "  {} {}",
            "Catalog Shift:".cyan(),
            format!("{:.2}", entry.integration_cost.catalog_shift)
        );
        println!(
            "  {} {}",
            "Entries Revised:".cyan(),
            entry.integration_cost.entries_revised
        );
        println!(
            "  {} {}",
            "References Broken:".cyan(),
            entry.integration_cost.references_broken
        );
        if entry.integration_cost.orphan {
            println!("  {} {}", "Status:".red().bold(), "ORPHAN");
        }

        // References
        if !entry.references.is_empty() {
            println!();
            println!("{}", "References:".yellow());
            for ref_id in &entry.references {
                println!("  - {}", ref_id);
            }
        }

        // Referenced by
        if !self.referenced_by.is_empty() {
            println!();
            println!("{}", "Referenced By:".yellow());
            for summary in &self.referenced_by {
                println!(
                    "  - {} {}",
                    summary.id,
                    summary
                        .topic
                        .as_ref()
                        .map(|t| format!("({})", t))
                        .unwrap_or_default()
                );
            }
        }

        // Revisions
        if !self.revisions.is_empty() {
            println!();
            println!("{}", "Revision History:".yellow());
            for (i, rev) in self.revisions.iter().enumerate() {
                println!(
                    "  {}. {} ({})",
                    i + 1,
                    rev.id,
                    format_timestamp(&rev.created)
                );
            }
        }
    }
}

/// Execute the read command.
pub async fn execute(base_url: &str, human: bool, args: ReadArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let mut url = format!(
        "{}/notebooks/{}/entries/{}",
        base_url, args.notebook_id, args.entry_id
    );

    if let Some(rev) = args.revision {
        url = format!("{}?revision={}", url, rev);
    }

    let response: ReadEntryResponse = make_request(&client, client.get(&url)).await?;

    output(&response, human)
}
