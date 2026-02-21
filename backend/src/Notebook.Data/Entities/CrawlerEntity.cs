namespace Notebook.Data.Entities;

/// <summary>
/// Generic crawler metadata (source-agnostic).
/// References implementation-specific state via state_provider and state_ref_id.
/// </summary>
public class CrawlerEntity
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public string Name { get; set; } = "";
    public string SourceType { get; set; } = ""; // confluence | git | filesystem

    // Implementation-specific state reference
    public string StateProvider { get; set; } = ""; // confluence_state | git_state | filesystem_state
    public Guid StateRefId { get; set; }

    // Configuration and tracking
    public bool IsEnabled { get; set; } = true;
    public string? ScheduleCron { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; } // success | failed | partial | pending
    public string? LastError { get; set; }

    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public Guid OrganizationId { get; set; }

    // Navigation properties
    public NotebookEntity? Notebook { get; set; }
    public ICollection<CrawlerRunEntity> Runs { get; set; } = new List<CrawlerRunEntity>();
}
