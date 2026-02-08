using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotebookAdmin.Models;

/// <summary>
/// Per-user resource quotas for the notebook platform.
/// </summary>
public class UserQuota
{
    /// <summary>
    /// User ID (PK, FK to AspNetUsers).
    /// </summary>
    [Key]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of notebooks the user can own.
    /// </summary>
    public int MaxNotebooks { get; set; } = 5;

    /// <summary>
    /// Maximum number of entries per notebook.
    /// </summary>
    public int MaxEntriesPerNotebook { get; set; } = 1000;

    /// <summary>
    /// Maximum size of a single entry in bytes.
    /// </summary>
    public long MaxEntrySizeBytes { get; set; } = 1_048_576; // 1 MB

    /// <summary>
    /// Maximum total storage in bytes across all notebooks.
    /// </summary>
    public long MaxTotalStorageBytes { get; set; } = 104_857_600; // 100 MB

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the owning user.
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
