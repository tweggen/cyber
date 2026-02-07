//! REVISE command - Update an existing entry.

use anyhow::Result;
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, make_request, output};

/// Arguments for the revise command.
#[derive(Args)]
pub struct ReviseArgs {
    /// Notebook ID containing the entry
    pub notebook_id: Uuid,

    /// Entry ID to revise
    pub entry_id: Uuid,

    /// New content for the entry (use @filename to read from file, or - for stdin)
    #[arg(short, long)]
    pub content: String,

    /// Reason for the revision (for audit purposes)
    #[arg(long)]
    pub reason: Option<String>,
}

/// Request body for revising an entry.
#[derive(Serialize)]
struct ReviseEntryRequest {
    content: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    reason: Option<String>,
}

/// Response from revising an entry.
#[derive(Debug, Deserialize, Serialize)]
pub struct ReviseEntryResponse {
    pub revision_id: Uuid,
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

impl HumanReadable for ReviseEntryResponse {
    fn print_human(&self) {
        println!("{}", "Entry revised successfully!".green().bold());
        println!();
        println!("  {} {}", "Revision ID:".cyan(), self.revision_id);
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
                "Revision marked as orphan (low coherence)"
            );
        }
    }
}

/// Execute the revise command.
pub async fn execute(base_url: &str, human: bool, args: ReviseArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let url = format!(
        "{}/notebooks/{}/entries/{}",
        base_url, args.notebook_id, args.entry_id
    );

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

    let request_body = ReviseEntryRequest {
        content,
        reason: args.reason,
    };

    let response: ReviseEntryResponse =
        make_request(&client, client.put(&url).json(&request_body)).await?;

    output(&response, human)
}
