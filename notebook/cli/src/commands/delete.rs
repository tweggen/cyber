//! DELETE command - Delete a notebook.

use anyhow::Result;
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{make_request, output, HumanReadable};

/// Arguments for the delete command.
#[derive(Args)]
pub struct DeleteArgs {
    /// Notebook ID to delete
    pub notebook_id: Uuid,

    /// Skip confirmation prompt (for non-interactive use)
    #[arg(long, short = 'y')]
    pub yes: bool,
}

/// Response from deleting a notebook.
#[derive(Debug, Deserialize, Serialize)]
pub struct DeleteNotebookResponse {
    pub id: Uuid,
    pub message: String,
}

impl HumanReadable for DeleteNotebookResponse {
    fn print_human(&self) {
        println!("{}", "Notebook deleted successfully!".green().bold());
        println!();
        println!("  {} {}", "ID:".cyan(), self.id);
    }
}

/// Execute the delete command.
pub async fn execute(base_url: &str, human: bool, args: DeleteArgs) -> Result<()> {
    // Confirmation prompt for interactive use
    if human && !args.yes {
        eprint!(
            "{} Are you sure you want to delete notebook {}? [y/N] ",
            "Warning:".yellow().bold(),
            args.notebook_id
        );

        use std::io::Write;
        std::io::stderr().flush()?;

        let mut input = String::new();
        std::io::stdin().read_line(&mut input)?;

        if !input.trim().eq_ignore_ascii_case("y") {
            eprintln!("Aborted.");
            return Ok(());
        }
    }

    let client = reqwest::Client::new();
    let url = format!("{}/notebooks/{}", base_url, args.notebook_id);

    let response: DeleteNotebookResponse = make_request(&client, client.delete(&url)).await?;

    output(&response, human)
}
