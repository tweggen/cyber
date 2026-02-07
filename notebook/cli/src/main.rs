//! Command-line interface for the Knowledge Exchange Platform.
//!
//! This CLI tool provides commands for all notebook operations:
//! - write: Create new entries
//! - revise: Update existing entries
//! - read: Retrieve entries with metadata
//! - browse: Get a catalog of notebook contents
//! - share: Manage access permissions
//! - observe: Watch for changes
//! - list: List accessible notebooks
//! - create: Create new notebooks
//! - delete: Delete notebooks
//!
//! Configuration via environment:
//! - NOTEBOOK_URL: Base URL of the notebook server (default: http://localhost:3000)
//! - NOTEBOOK_AUTHOR_ID: Author ID for authenticated operations (optional)

mod commands;

use clap::{Parser, Subcommand};

use commands::{
    browse::BrowseArgs, create::CreateArgs, delete::DeleteArgs, list::ListArgs,
    observe::ObserveArgs, read::ReadArgs, revise::ReviseArgs, share::ShareArgs, write::WriteArgs,
};

/// Knowledge Exchange Platform CLI
///
/// Interact with notebooks from the command line. Designed for both
/// AI agents (JSON output) and humans (--human flag for formatted output).
#[derive(Parser)]
#[command(name = "notebook")]
#[command(author, version, about, long_about = None)]
#[command(propagate_version = true)]
struct Cli {
    /// Output human-readable formatted text instead of JSON
    #[arg(long, global = true)]
    human: bool,

    /// Notebook server URL
    #[arg(
        long,
        env = "NOTEBOOK_URL",
        default_value = "http://localhost:3000",
        global = true
    )]
    url: String,

    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand)]
enum Commands {
    /// Create a new entry in a notebook
    Write(WriteArgs),

    /// Revise an existing entry
    Revise(ReviseArgs),

    /// Read an entry with its metadata
    Read(ReadArgs),

    /// Browse notebook contents (get catalog)
    Browse(BrowseArgs),

    /// Manage notebook access permissions
    Share(ShareArgs),

    /// Observe changes in a notebook
    Observe(ObserveArgs),

    /// List accessible notebooks
    List(ListArgs),

    /// Create a new notebook
    Create(CreateArgs),

    /// Delete a notebook
    Delete(DeleteArgs),
}

#[tokio::main]
async fn main() {
    let cli = Cli::parse();

    let result = match cli.command {
        Commands::Write(args) => commands::write::execute(&cli.url, cli.human, args).await,
        Commands::Revise(args) => commands::revise::execute(&cli.url, cli.human, args).await,
        Commands::Read(args) => commands::read::execute(&cli.url, cli.human, args).await,
        Commands::Browse(args) => commands::browse::execute(&cli.url, cli.human, args).await,
        Commands::Share(args) => commands::share::execute(&cli.url, cli.human, args).await,
        Commands::Observe(args) => commands::observe::execute(&cli.url, cli.human, args).await,
        Commands::List(args) => commands::list::execute(&cli.url, cli.human, args).await,
        Commands::Create(args) => commands::create::execute(&cli.url, cli.human, args).await,
        Commands::Delete(args) => commands::delete::execute(&cli.url, cli.human, args).await,
    };

    if let Err(e) = result {
        eprintln!("Error: {}", e);
        std::process::exit(1);
    }
}
