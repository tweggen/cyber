using System.ComponentModel.DataAnnotations;

namespace NotebookAdmin.Models;

/// <summary>
/// Organization-level default resource quotas for the notebook platform.
/// Individual users can inherit these defaults if they don't have custom quotas.
/// </summary>
public class OrganizationQuota
{
    /// <summary>
    /// Organization ID (PK, external reference to backend database).
    /// </summary>
    [Key]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Maximum number of notebooks a user in this organization can own.
    /// </summary>
    public int MaxNotebooks { get; set; } = 50;

    /// <summary>
    /// Maximum number of entries per notebook.
    /// </summary>
    public int MaxEntriesPerNotebook { get; set; } = 5000;

    /// <summary>
    /// Maximum size of a single entry in bytes.
    /// </summary>
    public long MaxEntrySizeBytes { get; set; } = 10_485_760; // 10 MB

    /// <summary>
    /// Maximum total storage in bytes across all notebooks.
    /// </summary>
    public long MaxTotalStorageBytes { get; set; } = 1_073_741_824; // 1 GB

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
