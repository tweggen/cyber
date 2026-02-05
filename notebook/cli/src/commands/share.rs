//! SHARE command - Manage notebook access permissions.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::{Args, Subcommand};
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{format_timestamp, make_request, output, HumanReadable};

/// Arguments for the share command.
#[derive(Args)]
pub struct ShareArgs {
    /// Notebook ID to manage access for
    pub notebook_id: Uuid,

    #[command(subcommand)]
    pub action: ShareAction,
}

#[derive(Subcommand)]
pub enum ShareAction {
    /// Grant access to an author
    Grant {
        /// Author ID to grant access to (64-character hex string)
        author_id: String,

        /// Grant read access
        #[arg(long, default_value = "true")]
        read: bool,

        /// Grant write access
        #[arg(long)]
        write: bool,
    },

    /// Revoke access from an author
    Revoke {
        /// Author ID to revoke access from (64-character hex string)
        author_id: String,
    },

    /// List participants with access to the notebook
    List,
}

/// Request body for granting access.
#[derive(Serialize)]
struct ShareRequest {
    author_id: String,
    permissions: Permissions,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Permissions {
    pub read: bool,
    pub write: bool,
}

/// Response from granting access.
#[derive(Debug, Deserialize, Serialize)]
pub struct ShareResponse {
    pub access_granted: bool,
    pub author_id: String,
    pub permissions: Permissions,
}

/// Response from revoking access.
#[derive(Debug, Deserialize, Serialize)]
pub struct RevokeResponse {
    pub access_revoked: bool,
    pub author_id: String,
}

/// Response from listing participants.
#[derive(Debug, Deserialize, Serialize)]
pub struct ParticipantsResponse {
    pub participants: Vec<Participant>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct Participant {
    pub author_id: String,
    pub permissions: Permissions,
    pub granted_at: DateTime<Utc>,
}

impl HumanReadable for ShareResponse {
    fn print_human(&self) {
        println!("{}", "Access granted successfully!".green().bold());
        println!();
        println!("  {} {}", "Author:".cyan(), &self.author_id[..16]);
        println!(
            "  {} read={}, write={}",
            "Permissions:".cyan(),
            if self.permissions.read { "yes" } else { "no" },
            if self.permissions.write { "yes" } else { "no" }
        );
    }
}

impl HumanReadable for RevokeResponse {
    fn print_human(&self) {
        println!("{}", "Access revoked successfully!".green().bold());
        println!();
        println!("  {} {}", "Author:".cyan(), &self.author_id[..16]);
    }
}

impl HumanReadable for ParticipantsResponse {
    fn print_human(&self) {
        println!("{}", "Notebook Participants".green().bold());
        println!("{}", "=".repeat(60));
        println!();

        if self.participants.is_empty() {
            println!("  {}", "(No participants)".dimmed());
            return;
        }

        println!(
            "  {:<20} {:<15} {}",
            "Author".cyan(),
            "Permissions".cyan(),
            "Granted".cyan()
        );
        println!("  {}", "-".repeat(55));

        for p in &self.participants {
            let perms = format!(
                "{}{}",
                if p.permissions.read { "R" } else { "-" },
                if p.permissions.write { "W" } else { "-" }
            );
            println!(
                "  {:<20} {:<15} {}",
                &p.author_id[..16],
                perms,
                format_timestamp(&p.granted_at)
            );
        }

        println!();
        println!("  {} {}", "Total:".cyan(), self.participants.len());
    }
}

/// Execute the share command.
pub async fn execute(base_url: &str, human: bool, args: ShareArgs) -> Result<()> {
    let client = reqwest::Client::new();

    match args.action {
        ShareAction::Grant {
            author_id,
            read,
            write,
        } => {
            let url = format!("{}/notebooks/{}/share", base_url, args.notebook_id);
            let request_body = ShareRequest {
                author_id,
                permissions: Permissions { read, write },
            };
            let response: ShareResponse =
                make_request(&client, client.post(&url).json(&request_body)).await?;
            output(&response, human)
        }

        ShareAction::Revoke { author_id } => {
            let url = format!(
                "{}/notebooks/{}/share/{}",
                base_url, args.notebook_id, author_id
            );
            let response: RevokeResponse = make_request(&client, client.delete(&url)).await?;
            output(&response, human)
        }

        ShareAction::List => {
            let url = format!("{}/notebooks/{}/participants", base_url, args.notebook_id);
            let response: ParticipantsResponse = make_request(&client, client.get(&url)).await?;
            output(&response, human)
        }
    }
}
