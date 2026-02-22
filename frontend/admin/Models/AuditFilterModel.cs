using System.ComponentModel.DataAnnotations;

namespace NotebookAdmin.Models;

/// <summary>
/// Advanced audit log filtering parameters.
/// </summary>
public class AuditFilterModel
{
    /// <summary>
    /// Start date (inclusive, UTC).
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// End date (inclusive, UTC).
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Filter by actor (user performing action).
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Filter by actor username.
    /// </summary>
    public string? ActorUsername { get; set; }

    /// <summary>
    /// Filter by action type (Create, Update, Delete, Lock, Unlock, etc.).
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Filter by target type (User, Notebook, Entry, Organization, etc.).
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Filter by target ID (specific resource).
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Filter by notebook ID.
    /// </summary>
    public Guid? NotebookId { get; set; }

    /// <summary>
    /// Filter by result status (Success, Failure).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Full-text search in action_details.
    /// </summary>
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Minimum severity level (0=Info, 1=Warning, 2=Error, 3=Critical).
    /// </summary>
    public int? MinSeverity { get; set; }

    /// <summary>
    /// Maximum results per page.
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Page number (0-indexed).
    /// </summary>
    public int PageNumber { get; set; } = 0;

    /// <summary>
    /// Sort field: timestamp, action, actor, target.
    /// </summary>
    public string SortBy { get; set; } = "timestamp";

    /// <summary>
    /// Sort direction: asc, desc.
    /// </summary>
    public string SortDirection { get; set; } = "desc";

    /// <summary>
    /// Returns true if any filter is applied.
    /// </summary>
    public bool HasFilters =>
        DateFrom.HasValue || DateTo.HasValue ||
        !string.IsNullOrEmpty(ActorId) || !string.IsNullOrEmpty(ActorUsername) ||
        !string.IsNullOrEmpty(Action) || !string.IsNullOrEmpty(TargetType) ||
        !string.IsNullOrEmpty(TargetId) || NotebookId.HasValue ||
        !string.IsNullOrEmpty(Status) || !string.IsNullOrEmpty(SearchQuery) ||
        MinSeverity.HasValue;
}

/// <summary>
/// Result of filtered audit query with metadata.
/// </summary>
public class AuditFilterResult
{
    /// <summary>
    /// Filtered audit entries.
    /// </summary>
    public required List<AuditLogEntryDto> Entries { get; init; }

    /// <summary>
    /// Total count of results (ignoring pagination).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total pages for pagination.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Current page number.
    /// </summary>
    public int CurrentPage { get; init; }

    /// <summary>
    /// Page size used.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Quick stats for filtered results.
    /// </summary>
    public required AuditStats Stats { get; init; }
}

/// <summary>
/// Statistics about filtered audit entries.
/// </summary>
public class AuditStats
{
    /// <summary>
    /// Total actions in filtered range.
    /// </summary>
    public int TotalActions { get; init; }

    /// <summary>
    /// Successful actions count.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Failed actions count.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Unique actors in range.
    /// </summary>
    public int UniqueActors { get; init; }

    /// <summary>
    /// Most common action.
    /// </summary>
    public string? MostCommonAction { get; init; }

    /// <summary>
    /// Date range of results.
    /// </summary>
    public DateTime? EarliestEntry { get; init; }
    public DateTime? LatestEntry { get; init; }
}
