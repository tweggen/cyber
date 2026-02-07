//! CREATE command - Create a new notebook.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{HumanReadable, format_timestamp, make_request, output};

/// Arguments for the create command.
#[derive(Args)]
pub struct CreateArgs {
    /// Name for the new notebook
    pub name: String,
}

/// Request body for creating a notebook.
#[derive(Serialize)]
struct CreateNotebookRequest {
    name: String,
}

/// Response from creating a notebook.
#[derive(Debug, Deserialize, Serialize)]
pub struct CreateNotebookResponse {
    pub id: Uuid,
    pub name: String,
    pub owner: String,
    pub created: DateTime<Utc>,
}

impl HumanReadable for CreateNotebookResponse {
    fn print_human(&self) {
        println!("{}", "Notebook created successfully!".green().bold());
        println!();
        println!("  {} {}", "ID:".cyan(), self.id);
        println!("  {} {}", "Name:".cyan(), self.name);
        println!("  {} {}", "Owner:".cyan(), &self.owner[..16]);
        println!(
            "  {} {}",
            "Created:".cyan(),
            format_timestamp(&self.created)
        );
    }
}

/// Execute the create command.
pub async fn execute(base_url: &str, human: bool, args: CreateArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let url = format!("{}/notebooks", base_url);

    let request_body = CreateNotebookRequest { name: args.name };

    let response: CreateNotebookResponse =
        make_request(&client, client.post(&url).json(&request_body)).await?;

    output(&response, human)
}
