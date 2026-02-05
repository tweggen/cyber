//! BROWSE command - Get a catalog of notebook contents.

use anyhow::Result;
use chrono::{DateTime, Utc};
use clap::Args;
use colored::Colorize;
use serde::{Deserialize, Serialize};
use uuid::Uuid;

use super::{make_request, output, truncate, HumanReadable};

/// Arguments for the browse command.
#[derive(Args)]
pub struct BrowseArgs {
    /// Notebook ID to browse
    pub notebook_id: Uuid,

    /// Search query to filter results
    #[arg(short, long)]
    pub query: Option<String>,

    /// Maximum tokens for the response (default: 4000)
    #[arg(long)]
    pub max_tokens: Option<usize>,
}

/// Response from browsing a notebook.
#[derive(Debug, Deserialize, Serialize)]
pub struct BrowseResponse {
    pub catalog: Vec<ClusterSummary>,
    pub notebook_entropy: f64,
    pub total_entries: u32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub query_matches: Option<usize>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub generated: Option<DateTime<Utc>>,
}

#[derive(Debug, Deserialize, Serialize)]
pub struct ClusterSummary {
    pub topic: String,
    pub summary: String,
    pub entry_count: u32,
    pub cumulative_cost: f64,
    #[serde(alias = "stability", alias = "latest_sequence")]
    pub latest_sequence: u64,
    #[serde(alias = "representative_entry_ids", alias = "entry_ids")]
    pub entry_ids: Vec<Uuid>,
}

impl HumanReadable for BrowseResponse {
    fn print_human(&self) {
        println!("{}", "Notebook Catalog".green().bold());
        println!("{}", "=".repeat(70));
        println!();

        println!(
            "  {} {}",
            "Total Entries:".cyan(),
            self.total_entries
        );
        println!(
            "  {} {:.1}",
            "Notebook Entropy:".cyan(),
            self.notebook_entropy
        );
        if let Some(matches) = self.query_matches {
            println!("  {} {}", "Query Matches:".cyan(), matches);
        }

        println!();
        println!("{}", "Topic Clusters:".yellow());
        println!("{}", "-".repeat(70));
        println!();

        for cluster in &self.catalog {
            // Topic header
            println!(
                "{} {} {}",
                ">>".blue().bold(),
                cluster.topic.bold(),
                format!("({} entries)", cluster.entry_count).dimmed()
            );

            // Summary (truncated for display)
            let summary = truncate(&cluster.summary, 200);
            for line in summary.lines().take(3) {
                println!("   {}", line);
            }

            // Metadata
            println!(
                "   {} {:.2}  {} {}",
                "Cost:".dimmed(),
                cluster.cumulative_cost,
                "Seq:".dimmed(),
                cluster.latest_sequence
            );

            println!();
        }

        if self.catalog.is_empty() {
            println!("  {}", "(No entries in notebook)".dimmed());
        }
    }
}

/// Execute the browse command.
pub async fn execute(base_url: &str, human: bool, args: BrowseArgs) -> Result<()> {
    let client = reqwest::Client::new();
    let mut url = format!("{}/notebooks/{}/browse", base_url, args.notebook_id);

    // Build query string
    let mut params = Vec::new();
    if let Some(query) = &args.query {
        params.push(format!("query={}", urlencoding::encode(query)));
    }
    if let Some(max_tokens) = args.max_tokens {
        params.push(format!("max_tokens={}", max_tokens));
    }
    if !params.is_empty() {
        url = format!("{}?{}", url, params.join("&"));
    }

    let response: BrowseResponse = make_request(&client, client.get(&url)).await?;

    output(&response, human)
}

/// URL encoding helper.
mod urlencoding {
    pub fn encode(s: &str) -> String {
        let mut result = String::new();
        for c in s.chars() {
            match c {
                'a'..='z' | 'A'..='Z' | '0'..='9' | '-' | '_' | '.' | '~' => {
                    result.push(c);
                }
                ' ' => result.push('+'),
                _ => {
                    for b in c.to_string().as_bytes() {
                        result.push_str(&format!("%{:02X}", b));
                    }
                }
            }
        }
        result
    }
}
