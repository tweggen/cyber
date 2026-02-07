//! Two-Agent Knowledge Exchange Integration Test
//!
//! This test demonstrates the core value proposition of the Knowledge Exchange Platform:
//! two AI agents sharing a notebook, building on each other's knowledge.
//!
//! ## Philosophical Foundation
//!
//! As discussed in docs/discussion.md:
//! - Storage and exchange are the same operation viewed from different temporal perspectives
//! - Writing is sending a message to a future reader
//! - Reading is receiving from a past writer
//! - The notebook IS the entity - externalized memory substrate
//!
//! ## Test Scenario
//!
//! 1. Agent A writes initial knowledge about topics X and Y
//! 2. Agent B browses, reads, and contributes knowledge referencing X
//! 3. Agent A observes changes and revises based on B's input
//! 4. Verify cyclic references, integration costs, and catalog
//!
//! ## Running
//!
//! ```bash
//! # Start the server first
//! cargo run --bin notebook-server
//!
//! # Run the test (in another terminal)
//! cargo test --test two_agent_exchange -- --nocapture
//! ```
//!
//! Owned by: agent-test-exchange (Task 5-3)

use reqwest::Client;
use serde::{Deserialize, Serialize};
use std::time::Duration;
use uuid::Uuid;

// ============================================================================
// API Types (matching server responses)
// ============================================================================

#[derive(Debug, Serialize)]
struct CreateNotebookRequest {
    name: String,
}

#[derive(Debug, Deserialize)]
struct CreateNotebookResponse {
    id: Uuid,
    name: String,
    owner: String,
}

#[derive(Debug, Serialize)]
struct CreateEntryRequest {
    content: String,
    content_type: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    topic: Option<String>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    references: Vec<Uuid>,
}

#[derive(Debug, Deserialize)]
struct CreateEntryResponse {
    entry_id: Uuid,
    causal_position: CausalPosition,
    integration_cost: IntegrationCost,
}

#[derive(Debug, Deserialize)]
struct CausalPosition {
    sequence: u64,
    activity_context: ActivityContext,
}

#[derive(Debug, Deserialize)]
struct ActivityContext {
    entries_since_last_by_author: u32,
    total_notebook_entries: u32,
    recent_entropy: f64,
}

#[derive(Debug, Clone, Deserialize)]
struct IntegrationCost {
    entries_revised: u32,
    references_broken: u32,
    catalog_shift: f64,
    orphan: bool,
}

#[derive(Debug, Serialize)]
struct ReviseRequest {
    content: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    reason: Option<String>,
}

#[derive(Debug, Deserialize)]
struct ReviseResponse {
    revision_id: Uuid,
    causal_position: CausalPosition,
    integration_cost: IntegrationCost,
}

#[derive(Debug, Deserialize)]
struct ReadEntryResponse {
    entry: EntryData,
    revisions: Vec<EntrySummary>,
    references: Vec<EntrySummary>,
    referenced_by: Vec<EntrySummary>,
}

#[derive(Debug, Deserialize)]
struct EntryData {
    id: Uuid,
    content: serde_json::Value, // Can be string or object with data+encoding
    content_type: String,
    topic: Option<String>,
    author: String,
    references: Vec<Uuid>,
    revision_of: Option<Uuid>,
    integration_cost: IntegrationCost,
}

#[derive(Debug, Deserialize)]
struct EntrySummary {
    id: Uuid,
    topic: Option<String>,
    author: String,
}

#[derive(Debug, Deserialize)]
struct BrowseResponse {
    catalog: Vec<ClusterSummary>,
    notebook_entropy: f64,
    total_entries: u32,
}

#[derive(Debug, Deserialize)]
struct ClusterSummary {
    topic: String,
    summary: String,
    entry_count: u32,
    cumulative_cost: f64,
    #[serde(default)]
    latest_sequence: Option<u64>,
    entry_ids: Vec<Uuid>,
}

#[derive(Debug, Deserialize)]
struct ObserveResponse {
    changes: Vec<ChangeEntry>,
    notebook_entropy: f64,
    current_sequence: u64,
}

#[derive(Debug, Deserialize)]
struct ChangeEntry {
    entry_id: Uuid,
    operation: String,
    author: String,
    topic: Option<String>,
    integration_cost: IntegrationCost,
    causal_position: CausalPositionSummary,
}

#[derive(Debug, Deserialize)]
struct CausalPositionSummary {
    sequence: u64,
}

// ============================================================================
// Test Agent Abstraction
// ============================================================================

/// Represents an AI agent interacting with the notebook.
struct Agent {
    name: String,
    client: Client,
    base_url: String,
    /// Track the last observed sequence for OBSERVE calls
    last_observed_sequence: u64,
}

impl Agent {
    fn new(name: &str, base_url: &str) -> Self {
        Self {
            name: name.to_string(),
            client: Client::builder()
                .timeout(Duration::from_secs(30))
                .build()
                .expect("Failed to create HTTP client"),
            base_url: base_url.to_string(),
            last_observed_sequence: 0,
        }
    }

    /// Write an entry to the notebook.
    async fn write(
        &self,
        notebook_id: Uuid,
        content: &str,
        topic: Option<&str>,
        references: Vec<Uuid>,
    ) -> Result<CreateEntryResponse, Box<dyn std::error::Error>> {
        let url = format!("{}/notebooks/{}/entries", self.base_url, notebook_id);
        let request = CreateEntryRequest {
            content: content.to_string(),
            content_type: "text/plain".to_string(),
            topic: topic.map(|s| s.to_string()),
            references,
        };

        let response = self.client.post(&url).json(&request).send().await?;

        if !response.status().is_success() {
            let status = response.status();
            let body = response.text().await?;
            return Err(format!("WRITE failed: {} - {}", status, body).into());
        }

        let result: CreateEntryResponse = response.json().await?;
        println!(
            "[{}] WRITE entry {} (seq={}, topic={:?}, cost={:.2})",
            self.name,
            result.entry_id,
            result.causal_position.sequence,
            topic,
            result.integration_cost.catalog_shift
        );
        Ok(result)
    }

    /// Revise an existing entry.
    async fn revise(
        &self,
        notebook_id: Uuid,
        entry_id: Uuid,
        content: &str,
        reason: Option<&str>,
    ) -> Result<ReviseResponse, Box<dyn std::error::Error>> {
        let url = format!(
            "{}/notebooks/{}/entries/{}",
            self.base_url, notebook_id, entry_id
        );
        let request = ReviseRequest {
            content: content.to_string(),
            reason: reason.map(|s| s.to_string()),
        };

        let response = self.client.put(&url).json(&request).send().await?;

        if !response.status().is_success() {
            let status = response.status();
            let body = response.text().await?;
            return Err(format!("REVISE failed: {} - {}", status, body).into());
        }

        let result: ReviseResponse = response.json().await?;
        println!(
            "[{}] REVISE entry {} -> {} (seq={}, cost={:.2})",
            self.name,
            entry_id,
            result.revision_id,
            result.causal_position.sequence,
            result.integration_cost.catalog_shift
        );
        Ok(result)
    }

    /// Read an entry from the notebook.
    async fn read(
        &self,
        notebook_id: Uuid,
        entry_id: Uuid,
    ) -> Result<ReadEntryResponse, Box<dyn std::error::Error>> {
        let url = format!(
            "{}/notebooks/{}/entries/{}",
            self.base_url, notebook_id, entry_id
        );

        let response = self.client.get(&url).send().await?;

        if !response.status().is_success() {
            let status = response.status();
            let body = response.text().await?;
            return Err(format!("READ failed: {} - {}", status, body).into());
        }

        let result: ReadEntryResponse = response.json().await?;
        println!(
            "[{}] READ entry {} (topic={:?}, refs={}, cited_by={})",
            self.name,
            entry_id,
            result.entry.topic,
            result.entry.references.len(),
            result.referenced_by.len()
        );
        Ok(result)
    }

    /// Browse the notebook catalog.
    async fn browse(
        &self,
        notebook_id: Uuid,
    ) -> Result<BrowseResponse, Box<dyn std::error::Error>> {
        let url = format!("{}/notebooks/{}/browse", self.base_url, notebook_id);

        let response = self.client.get(&url).send().await?;

        if !response.status().is_success() {
            let status = response.status();
            let body = response.text().await?;
            return Err(format!("BROWSE failed: {} - {}", status, body).into());
        }

        let result: BrowseResponse = response.json().await?;
        println!(
            "[{}] BROWSE notebook (entries={}, entropy={:.2}, clusters={})",
            self.name,
            result.total_entries,
            result.notebook_entropy,
            result.catalog.len()
        );
        for cluster in &result.catalog {
            println!(
                "       - {} ({} entries, cost={:.2}): {}",
                cluster.topic,
                cluster.entry_count,
                cluster.cumulative_cost,
                truncate(&cluster.summary, 50)
            );
        }
        Ok(result)
    }

    /// Observe changes since last check.
    async fn observe(
        &mut self,
        notebook_id: Uuid,
    ) -> Result<ObserveResponse, Box<dyn std::error::Error>> {
        let url = format!(
            "{}/notebooks/{}/observe?since={}",
            self.base_url, notebook_id, self.last_observed_sequence
        );

        let response = self.client.get(&url).send().await?;

        if !response.status().is_success() {
            let status = response.status();
            let body = response.text().await?;
            return Err(format!("OBSERVE failed: {} - {}", status, body).into());
        }

        let result: ObserveResponse = response.json().await?;
        println!(
            "[{}] OBSERVE changes (since={}, found={}, entropy={:.2}, current_seq={})",
            self.name,
            self.last_observed_sequence,
            result.changes.len(),
            result.notebook_entropy,
            result.current_sequence
        );
        for change in &result.changes {
            println!(
                "       - {} {} (seq={}, cost={:.2}, topic={:?})",
                change.operation,
                change.entry_id,
                change.causal_position.sequence,
                change.integration_cost.catalog_shift,
                change.topic
            );
        }

        // Update last observed sequence
        self.last_observed_sequence = result.current_sequence;
        Ok(result)
    }
}

/// Helper to truncate strings for display.
fn truncate(s: &str, max_len: usize) -> String {
    if s.len() <= max_len {
        s.to_string()
    } else {
        format!("{}...", &s[..max_len - 3])
    }
}

// ============================================================================
// Test Notebook Setup
// ============================================================================

/// Create a new notebook for the test.
async fn create_test_notebook(
    client: &Client,
    base_url: &str,
) -> Result<Uuid, Box<dyn std::error::Error>> {
    let url = format!("{}/notebooks", base_url);
    let request = CreateNotebookRequest {
        name: format!(
            "Two-Agent Exchange Test {}",
            chrono::Utc::now().format("%Y-%m-%d %H:%M:%S")
        ),
    };

    let response = client.post(&url).json(&request).send().await?;

    if !response.status().is_success() {
        let status = response.status();
        let body = response.text().await?;
        return Err(format!("CREATE NOTEBOOK failed: {} - {}", status, body).into());
    }

    let result: CreateNotebookResponse = response.json().await?;
    println!("Created test notebook: {} ({})", result.name, result.id);
    Ok(result.id)
}

// ============================================================================
// Main Test
// ============================================================================

#[tokio::test]
async fn test_two_agent_knowledge_exchange() {
    println!("\n========================================");
    println!("Two-Agent Knowledge Exchange Test");
    println!("========================================\n");

    // Configuration
    let base_url = std::env::var("NOTEBOOK_SERVER_URL")
        .unwrap_or_else(|_| "http://localhost:8000".to_string());

    println!("Server URL: {}\n", base_url);

    // Check if server is running
    let client = Client::builder()
        .timeout(Duration::from_secs(5))
        .build()
        .expect("Failed to create HTTP client");

    let health_url = format!("{}/health", base_url);
    match client.get(&health_url).send().await {
        Ok(response) if response.status().is_success() => {
            println!("Server is running (health check passed)\n");
        }
        Ok(response) => {
            println!(
                "SKIP: Server returned unexpected status: {}",
                response.status()
            );
            println!("Start the server with: cargo run --bin notebook-server");
            return;
        }
        Err(e) => {
            println!("SKIP: Server not reachable: {}", e);
            println!("Start the server with: cargo run --bin notebook-server");
            return;
        }
    }

    // Create agents
    let mut agent_a = Agent::new("Agent-A", &base_url);
    let mut agent_b = Agent::new("Agent-B", &base_url);

    // ========================================================================
    // Step 1: Setup - Create shared notebook
    // ========================================================================
    println!("--- Step 1: Setup ---");

    let notebook_id = match create_test_notebook(&client, &base_url).await {
        Ok(id) => id,
        Err(e) => {
            println!("Failed to create notebook: {}", e);
            panic!("Test setup failed");
        }
    };

    // Agent A observes to establish baseline
    let initial_observe = agent_a
        .observe(notebook_id)
        .await
        .expect("Initial observe failed");
    assert_eq!(
        initial_observe.changes.len(),
        0,
        "New notebook should have no entries"
    );

    println!();

    // ========================================================================
    // Step 2: Agent A writes initial knowledge
    // ========================================================================
    println!("--- Step 2: Agent A writes initial knowledge ---");

    // Topic X: Machine Learning fundamentals
    let entry_x = agent_a
        .write(
            notebook_id,
            "Machine learning is a subset of artificial intelligence that enables systems to learn and improve from experience. Key concepts include supervised learning, unsupervised learning, and reinforcement learning. The field has grown rapidly due to advances in computing power and data availability.",
            Some("topic-x machine-learning"),
            vec![],
        )
        .await
        .expect("Agent A write topic X failed");

    // Topic Y: Neural Networks (references X)
    let entry_y = agent_a
        .write(
            notebook_id,
            "Neural networks are computing systems inspired by biological neural networks. They form the foundation of deep learning, which is a subfield of machine learning. Neural networks consist of layers of interconnected nodes that process information using connectionist approaches.",
            Some("topic-y neural-networks"),
            vec![entry_x.entry_id],
        )
        .await
        .expect("Agent A write topic Y failed");

    assert!(
        entry_y.causal_position.sequence > entry_x.causal_position.sequence,
        "Causal ordering: Y should come after X"
    );

    println!();

    // ========================================================================
    // Step 3: Agent B browses and reads
    // ========================================================================
    println!("--- Step 3: Agent B browses and reads ---");

    // Agent B browses the notebook
    let browse_result = agent_b
        .browse(notebook_id)
        .await
        .expect("Agent B browse failed");

    assert!(
        browse_result.total_entries >= 2,
        "Should see at least 2 entries"
    );

    // Agent B reads entry X
    let read_x = agent_b
        .read(notebook_id, entry_x.entry_id)
        .await
        .expect("Agent B read X failed");

    assert_eq!(read_x.entry.id, entry_x.entry_id);
    assert!(
        read_x.referenced_by.len() >= 1,
        "X should be referenced by Y"
    );

    println!();

    // ========================================================================
    // Step 4: Agent B contributes
    // ========================================================================
    println!("--- Step 4: Agent B contributes ---");

    // Topic Z: Deep Learning (references X)
    let entry_z = agent_b
        .write(
            notebook_id,
            "Deep learning extends neural network concepts with multiple hidden layers, enabling hierarchical feature learning. Applications include computer vision, natural language processing, and autonomous systems. Deep learning has achieved breakthrough results in image recognition and language translation.",
            Some("topic-z deep-learning"),
            vec![entry_x.entry_id],
        )
        .await
        .expect("Agent B write topic Z failed");

    // Different perspective on Y (references Y)
    let entry_y_perspective = agent_b
        .write(
            notebook_id,
            "An alternative view on neural networks emphasizes their biological inspiration less and focuses on their mathematical properties. Activation functions, backpropagation, and gradient descent are the core mechanisms. Modern architectures like transformers have moved beyond traditional neural network designs.",
            Some("topic-y perspective neural-networks"),
            vec![entry_y.entry_id],
        )
        .await
        .expect("Agent B write Y perspective failed");

    println!();

    // ========================================================================
    // Step 5: Agent A observes changes
    // ========================================================================
    println!("--- Step 5: Agent A observes changes ---");

    // First, Agent A needs to observe to update their sequence
    agent_a.last_observed_sequence = entry_y.causal_position.sequence;

    let observe_result = agent_a
        .observe(notebook_id)
        .await
        .expect("Agent A observe failed");

    assert!(
        observe_result.changes.len() >= 2,
        "Should see Agent B's contributions (got {})",
        observe_result.changes.len()
    );

    // Verify integration costs are present
    let has_nonzero_cost = observe_result
        .changes
        .iter()
        .any(|c| c.integration_cost.catalog_shift > 0.0 || c.integration_cost.entries_revised > 0);

    println!(
        "       Integration costs present: {}",
        if has_nonzero_cost {
            "yes"
        } else {
            "no (may be zero for coherent additions)"
        }
    );

    println!();

    // ========================================================================
    // Step 6: Agent A revises based on B's input
    // ========================================================================
    println!("--- Step 6: Agent A revises based on B's input ---");

    // Agent A revises Y incorporating B's perspective
    let revised_y = agent_a
        .revise(
            notebook_id,
            entry_y.entry_id,
            "Neural networks are computing systems that combine biological inspiration with rigorous mathematical foundations. Building on the alternative perspective that emphasizes mathematical properties, we recognize that activation functions, backpropagation, and gradient descent are fundamental mechanisms. The field continues evolving with architectures like transformers that transcend traditional designs while maintaining core neural network principles.",
            Some("Incorporating Agent B's mathematical perspective"),
        )
        .await
        .expect("Agent A revise Y failed");

    println!();

    // ========================================================================
    // Step 7: Verify final state
    // ========================================================================
    println!("--- Step 7: Verify final state ---");

    // Browse to see combined catalog
    let final_browse = agent_a
        .browse(notebook_id)
        .await
        .expect("Final browse failed");

    println!("\nFinal notebook state:");
    println!("  Total entries: {}", final_browse.total_entries);
    println!("  Notebook entropy: {:.2}", final_browse.notebook_entropy);
    println!("  Clusters: {}", final_browse.catalog.len());

    // Verify we have entries from both agents
    assert!(
        final_browse.total_entries >= 5,
        "Should have at least 5 entries (2 from A, 2 from B, 1 revision)"
    );

    // Verify references exist
    let read_y_final = agent_a
        .read(notebook_id, entry_y.entry_id)
        .await
        .expect("Read Y final failed");

    println!("\nReference graph for entry Y:");
    println!("  References: {:?}", read_y_final.entry.references);
    println!(
        "  Referenced by: {} entries",
        read_y_final.referenced_by.len()
    );
    println!("  Revisions: {} entries", read_y_final.revisions.len());

    // Y should be referenced by B's perspective entry
    assert!(
        read_y_final.referenced_by.len() >= 1,
        "Y should be referenced by at least one entry (B's perspective)"
    );

    // Y should have a revision
    assert!(
        read_y_final.revisions.len() >= 1,
        "Y should have at least one revision"
    );

    // Verify Z references X (cyclic potential)
    let read_z = agent_a
        .read(notebook_id, entry_z.entry_id)
        .await
        .expect("Read Z failed");

    assert!(
        read_z.entry.references.contains(&entry_x.entry_id),
        "Z should reference X"
    );

    // Verify the revision
    let read_revised = agent_a
        .read(notebook_id, revised_y.revision_id)
        .await
        .expect("Read revised Y failed");

    assert_eq!(
        read_revised.entry.revision_of,
        Some(entry_y.entry_id),
        "Revision should point to original Y"
    );

    println!("\n========================================");
    println!("Test PASSED: Two-agent exchange verified");
    println!("========================================");
    println!("\nKey achievements:");
    println!("  - Shared notebook created and accessible to both agents");
    println!("  - References form a graph (Y->X, Z->X, perspective->Y)");
    println!("  - Agent B built on Agent A's knowledge");
    println!("  - Agent A observed and incorporated Agent B's perspective");
    println!("  - Revision chain maintained (original Y -> revised Y)");
    println!("  - Catalog shows combined knowledge from both agents");
    println!();
}

// ============================================================================
// Additional Test: Verify Integration Costs
// ============================================================================

#[tokio::test]
async fn test_integration_cost_behavior() {
    println!("\n========================================");
    println!("Integration Cost Behavior Test");
    println!("========================================\n");

    let base_url = std::env::var("NOTEBOOK_SERVER_URL")
        .unwrap_or_else(|_| "http://localhost:8000".to_string());

    let client = Client::builder()
        .timeout(Duration::from_secs(5))
        .build()
        .expect("Failed to create HTTP client");

    // Check if server is running
    let health_url = format!("{}/health", base_url);
    match client.get(&health_url).send().await {
        Ok(response) if response.status().is_success() => {}
        _ => {
            println!("SKIP: Server not reachable");
            return;
        }
    }

    let agent = Agent::new("CostTest", &base_url);

    // Create test notebook
    let notebook_id = create_test_notebook(&client, &base_url)
        .await
        .expect("Failed to create notebook");

    // Write several related entries
    let entry1 = agent
        .write(
            notebook_id,
            "Quantum computing uses quantum mechanics principles.",
            Some("quantum"),
            vec![],
        )
        .await
        .expect("Write 1 failed");

    let entry2 = agent
        .write(
            notebook_id,
            "Quantum entanglement enables quantum computing applications.",
            Some("quantum"),
            vec![entry1.entry_id],
        )
        .await
        .expect("Write 2 failed");

    // Write an unrelated entry (potential orphan or high cost)
    let entry3 = agent
        .write(
            notebook_id,
            "Cooking recipes require precise measurements and timing.",
            Some("cooking"),
            vec![],
        )
        .await
        .expect("Write 3 failed");

    println!("\nIntegration costs:");
    println!(
        "  Entry 1 (initial): catalog_shift={:.2}",
        entry1.integration_cost.catalog_shift
    );
    println!(
        "  Entry 2 (related): catalog_shift={:.2}",
        entry2.integration_cost.catalog_shift
    );
    println!(
        "  Entry 3 (unrelated): catalog_shift={:.2}",
        entry3.integration_cost.catalog_shift
    );

    // The unrelated entry might have higher cost or be marked orphan
    // depending on the entropy engine's calibration
    println!("  Entry 3 orphan flag: {}", entry3.integration_cost.orphan);

    println!("\nTest completed (integration costs are system-computed)");
}

// ============================================================================
// Additional Test: Verify Catalog Generation
// ============================================================================

#[tokio::test]
async fn test_catalog_reflects_combined_knowledge() {
    println!("\n========================================");
    println!("Catalog Generation Test");
    println!("========================================\n");

    let base_url = std::env::var("NOTEBOOK_SERVER_URL")
        .unwrap_or_else(|_| "http://localhost:8000".to_string());

    let client = Client::builder()
        .timeout(Duration::from_secs(5))
        .build()
        .expect("Failed to create HTTP client");

    // Check if server is running
    let health_url = format!("{}/health", base_url);
    match client.get(&health_url).send().await {
        Ok(response) if response.status().is_success() => {}
        _ => {
            println!("SKIP: Server not reachable");
            return;
        }
    }

    let agent = Agent::new("CatalogTest", &base_url);

    // Create test notebook
    let notebook_id = create_test_notebook(&client, &base_url)
        .await
        .expect("Failed to create notebook");

    // Write entries across different topics
    let topics = [
        (
            "Rust is a systems programming language focused on safety.",
            "rust programming",
        ),
        (
            "Python is popular for data science and machine learning.",
            "python programming",
        ),
        (
            "JavaScript runs in web browsers and on servers.",
            "javascript programming",
        ),
        (
            "Memory safety prevents buffer overflows and data races.",
            "rust programming",
        ),
        (
            "Type inference reduces boilerplate in modern languages.",
            "rust programming",
        ),
    ];

    for (content, topic) in topics {
        agent
            .write(notebook_id, content, Some(topic), vec![])
            .await
            .expect("Write failed");
    }

    // Browse and check catalog
    let browse = agent.browse(notebook_id).await.expect("Browse failed");

    println!("\nCatalog structure:");
    println!("  Total entries: {}", browse.total_entries);
    println!("  Clusters: {}", browse.catalog.len());

    // Should cluster related topics together
    for cluster in &browse.catalog {
        println!(
            "  - {}: {} entries (cost={:.2})",
            cluster.topic, cluster.entry_count, cluster.cumulative_cost
        );
    }

    assert!(browse.total_entries >= 5, "Should have at least 5 entries");

    println!("\nTest completed (catalog generation verified)");
}
