//! LIST command - List accessible notebooks.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, format_timestamp, make_request, output};

/// Arguments for the list command.
#[derive(Args)]
pub struct ListArgs {
    // No additional arguments needed
}

/// Response from listing notebooks (Rust server format).
#[derive(Debug, Deserialize, Serialize)]
pub struct ListNotebooksResponse {
    pub notebooks: Vec<NotebookSummary>,
}

/// Notebook summary - handles both Rust server and bootstrap server formats.
#[derive(Debug, Deserialize, Serialize)]
pub struct NotebookSummary {
    pub id: Uuid,
    pub name: String,
    pub owner: String,
    #[serde(default)]
    pub is_owner: bool,
    #[serde(default)]
    pub permissions: Option<NotebookPermissions>,
    #[serde(default)]
    pub total_entries: i64,
    #[serde(default)]
    pub total_entropy: f64,
    #[serde(default)]
    pub last_activity_sequence: i64,
    #[serde(default)]
    pub participant_count: i64,
    /// Bootstrap server format: participants list
    #[serde(default)]
    pub participants: Vec<BootstrapParticipant>,
    /// Bootstrap server format: created timestamp
    #[serde(default)]
    pub created: Option<DateTime<Utc>>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct NotebookPermissions {
    pub read: bool,
    pub write: bool,
}

/// Bootstrap server participant format.
#[derive(Debug, Deserialize, Serialize)]
pub struct BootstrapParticipant {
    pub entity: String,
    pub read: bool,
    pub write: bool,
}

impl HumanReadable for ListNotebooksResponse {
    fn print_human(&self) {
        println!("{}", "Accessible Notebooks".green().bold());
        println!("{}", "=".repeat(80));
        println!();

        if self.notebooks.is_empty() {
            println!("  {}", "(No notebooks accessible)".dimmed());
            return;
        }

        for notebook in &self.notebooks {
            // Ownership indicator
            let owner_indicator = if notebook.is_owner {
                "*".yellow()
            } else {
                " ".normal()
            };

            // Permissions - handle both formats
            let perms = if let Some(ref p) = notebook.permissions {
                format!(
                    "[{}{}]",
                    if p.read { "R" } else { "-" },
                    if p.write { "W" } else { "-" }
                )
            } else if !notebook.participants.is_empty() {
                // Bootstrap format - show participant count
                format!("[{} participants]", notebook.participants.len())
            } else {
                String::new()
            };

            println!(
                "  {} {} {}",
                owner_indicator,
                notebook.name.bold(),
                perms.dimmed()
            );
            println!("    {} {}", "ID:".cyan(), notebook.id);
            println!("    {} {}", "Owner:".cyan(), notebook.owner);

            // Show stats if available (Rust server format)
            if notebook.total_entries > 0 || notebook.total_entropy > 0.0 {
                println!(
                    "    {} {} entries, {:.1} entropy, {} participants",
                    "Stats:".cyan(),
                    notebook.total_entries,
                    notebook.total_entropy,
                    notebook.participant_count
                );
                println!(
                    "    {} sequence {}",
                    "Last Activity:".cyan(),
                    notebook.last_activity_sequence
                );
            }

            // Show created timestamp if available (bootstrap format)
            if let Some(created) = &notebook.created {
                println!("    {} {}", "Created:".cyan(), format_timestamp(created));
            }

            println!();
        }

        println!("  {} {}", "Total:".cyan(), self.notebooks.len());
        println!();
        println!("  {}", "* = You are the owner".dimmed());
    }
}

/// Execute the list command.
pub async fn execute(base_url: &str, human: bool, _args: ListArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let url = format!("{}/notebooks", base_url);

    let response: ListNotebooksResponse = make_request(&client, client.get(&url)).await?;

    output(&response, human)
}
