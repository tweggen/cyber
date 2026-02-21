//! RENAME command - Rename an existing notebook.

use anyhow::Result;
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, make_request, output};

/// Arguments for the rename command.
#[derive(Args)]
pub struct RenameArgs {
    /// Notebook ID to rename
    pub notebook_id: Uuid,

    /// New name for the notebook
    pub name: String,
}

/// Request body for renaming a notebook.
#[derive(Serialize)]
struct RenameNotebookRequest {
    name: String,
}

/// Response from renaming a notebook.
#[derive(Debug, Deserialize, Serialize)]
pub struct RenameNotebookResponse {
    pub id: Uuid,
    pub name: String,
}

impl HumanReadable for RenameNotebookResponse {
    fn print_human(&self) {
        println!("{}", "Notebook renamed successfully!".green().bold());
        println!();
        println!("  {} {}", "ID:".cyan(), self.id);
        println!("  {} {}", "Name:".cyan(), self.name);
    }
}

/// Execute the rename command.
pub async fn execute(client: &reqwest::Client, base_url: &str, human: bool, args: RenameArgs) -> Result<()> {
    let url = format!("{}/notebooks/{}", base_url, args.notebook_id);

    let request_body = RenameNotebookRequest { name: args.name };

    let response: RenameNotebookResponse =
        make_request(client, client.patch(&url).json(&request_body)).await?;

    output(&response, human)
}
