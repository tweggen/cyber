namespace Notebook.Data.Entities;

/// <summary>
/// Crawler execution history and results.
/// </summary>
public class CrawlerRunEntity
{
    public Guid Id { get; set; }
    public Guid CrawlerId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running"; // running | success | failed | partial
    public int EntriesCreated { get; set; }
    public int EntriesUpdated { get; set; }
    public int EntriesUnchanged { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Stats { get; set; } // JSON: {duration_ms, bytes_processed, pages_fetched, ...}

    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public CrawlerEntity? Crawler { get; set; }
}
