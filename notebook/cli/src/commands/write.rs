//! WRITE command - Create a new entry in a notebook.

use anyhow::Result;
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, make_request, output};

/// Arguments for the write command.
#[derive(Args)]
pub struct WriteArgs {
    /// Notebook ID to write to
    pub notebook_id: Uuid,

    /// Content of the entry (use @filename to read from file, or - for stdin)
    #[arg(short, long)]
    pub content: String,

    /// Content type (MIME type)
    #[arg(short = 't', long, default_value = "text/plain")]
    pub content_type: String,

    /// Optional topic/category for the entry
    #[arg(long)]
    pub topic: Option<String>,

    /// References to other entries (UUIDs, can be specified multiple times)
    #[arg(short, long)]
    pub reference: Vec<Uuid>,
}

/// Request body for creating an entry.
#[derive(Serialize)]
struct CreateEntryRequest {
    content: String,
    content_type: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    topic: Option<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    references: Vec<Uuid>,
}

/// Response from creating an entry.
#[derive(Debug, Deserialize, Serialize)]
pub struct CreateEntryResponse {
    pub entry_id: Uuid,
    pub causal_position: CausalPosition,
    pub integration_cost: IntegrationCost,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct CausalPosition {
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

impl HumanReadable for CreateEntryResponse {
    fn print_human(&self) {
        println!("{}", "Entry created successfully!".green().bold());
        println!();
        println!("  {} {}", "Entry ID:".cyan(), self.entry_id);
        println!("  {} {}", "Sequence:".cyan(), self.causal_position.sequence);
        println!();
        println!("{}", "Integration Cost:".yellow());
        println!(
            "  {} {}",
            "Catalog Shift:".cyan(),
            format!("{:.2}", self.integration_cost.catalog_shift)
        );
        println!(
            "  {} {}",
            "Entries Revised:".cyan(),
            self.integration_cost.entries_revised
        );
        if self.integration_cost.orphan {
            println!(
                "  {} {}",
                "Warning:".red().bold(),
                "Entry marked as orphan (low coherence)"
            );
        }
    }
}

/// Execute the write command.
pub async fn execute(client: &reqwest::Client, base_url: &str, human: bool, args: WriteArgs) -> Result<()> {
    let url = format!("{}/notebooks/{}/entries", base_url, args.notebook_id);

    // Handle special content sources
    let content = if args.content == "-" {
        // Read from stdin
        use std::io::Read;
        let mut buffer = String::new();
        std::io::stdin().read_to_string(&mut buffer)?;
        buffer
    } else if args.content.starts_with('@') {
        // Read from file
        let path = &args.content[1..];
        std::fs::read_to_string(path)?
    } else {
        args.content
    };

    let request_body = CreateEntryRequest {
        content,
        content_type: args.content_type,
        topic: args.topic,
        references: args.reference,
    };

    let response: CreateEntryResponse =
        make_request(client, client.post(&url).json(&request_body)).await?;

    output(&response, human)
}
