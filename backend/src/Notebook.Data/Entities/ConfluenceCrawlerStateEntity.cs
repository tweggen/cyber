namespace Notebook.Data.Entities;

/// <summary>
/// Confluence crawler implementation-specific state.
/// Referenced by CrawlerEntity when source_type='confluence' and state_provider='confluence_state'.
/// </summary>
public class ConfluenceCrawlerStateEntity
{
    public Guid Id { get; set; }

    // User-provided configuration (validated JSON)
    public string Config { get; set; } = "{}";

    // Incremental sync state (updated after each sync)
    public string SyncState { get; set; } = "{}";

    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
