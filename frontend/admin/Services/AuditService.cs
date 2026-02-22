using NotebookAdmin.Models;
using System.Text;

namespace NotebookAdmin.Services;

/// <summary>
/// Service for querying, filtering, and exporting audit logs.
/// </summary>
public class AuditService
{
    private readonly NotebookApiClient _apiClient;
    private readonly ILogger<AuditService> _logger;

    public AuditService(NotebookApiClient apiClient, ILogger<AuditService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Query audit logs with advanced filtering.
    /// </summary>
    public async Task<AuditFilterResult> GetFilteredAuditAsync(
        string adminAuthorId,
        AuditFilterModel filter)
    {
        try
        {
            // Call backend API
            var response = await _apiClient.QueryGlobalAuditAsync(
                adminAuthorId,
                actor: filter.ActorId,
                action: filter.Action,
                limit: 1000);

            if (response?.Entries == null)
                return new AuditFilterResult
                {
                    Entries = new List<AuditLogEntryDto>(),
                    TotalCount = 0,
                    TotalPages = 0,
                    CurrentPage = filter.PageNumber,
                    PageSize = filter.PageSize,
                    Stats = new AuditStats()
                };

            // Apply client-side filtering (for fields not in API)
            var filtered = ApplyClientFilters(response.Entries, filter).ToList();

            // Calculate pagination
            var totalCount = filtered.Count;
            var totalPages = (totalCount + filter.PageSize - 1) / filter.PageSize;

            // Get page results
            var pageResults = filtered
                .Skip(filter.PageNumber * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            // Calculate stats
            var stats = CalculateStats(filtered);

            _logger.LogInformation(
                "Audit query: {Count} results, page {Page}/{Pages}",
                totalCount, filter.PageNumber + 1, totalPages);

            return new AuditFilterResult
            {
                Entries = pageResults,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = filter.PageNumber,
                PageSize = filter.PageSize,
                Stats = stats
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit filter query failed");
            throw;
        }
    }

    /// <summary>
    /// Export audit logs as CSV.
    /// </summary>
    public async Task<string> ExportAuditCsvAsync(
        string adminAuthorId,
        AuditFilterModel filter)
    {
        var result = await GetFilteredAuditAsync(adminAuthorId, filter);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(CsvEncodeLine(new[]
        {
            "timestamp",
            "actor_id",
            "action",
            "target_type",
            "target_id",
            "notebook_id",
            "details"
        }));

        // Data rows
        foreach (var entry in result.Entries)
        {
            sb.AppendLine(CsvEncodeLine(new[]
            {
                entry.Timestamp.ToString("O"),
                entry.AuthorId ?? "",
                entry.Action ?? "",
                entry.TargetType ?? "",
                entry.TargetId ?? "",
                entry.NotebookId?.ToString() ?? "",
                entry.Detail?.ToString() ?? ""
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export audit logs as JSON.
    /// </summary>
    public async Task<string> ExportAuditJsonAsync(
        string adminAuthorId,
        AuditFilterModel filter)
    {
        var result = await GetFilteredAuditAsync(adminAuthorId, filter);
        return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private IEnumerable<AuditLogEntryDto> ApplyClientFilters(
        List<AuditLogEntryDto> entries,
        AuditFilterModel filter)
    {
        var query = entries.AsEnumerable();

        // Date range
        if (filter.DateFrom.HasValue)
            query = query.Where(e => e.Timestamp >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(e => e.Timestamp <= filter.DateTo.Value.AddDays(1)); // Include entire end day

        // Actor username
        if (!string.IsNullOrEmpty(filter.ActorUsername))
            query = query.Where(e => (e.AuthorId ?? "").Contains(filter.ActorUsername, StringComparison.OrdinalIgnoreCase));

        // Target filters
        if (!string.IsNullOrEmpty(filter.TargetType))
            query = query.Where(e => e.TargetType == filter.TargetType);
        if (!string.IsNullOrEmpty(filter.TargetId))
            query = query.Where(e => (e.TargetId ?? "").Contains(filter.TargetId, StringComparison.OrdinalIgnoreCase));
        if (filter.NotebookId.HasValue)
            query = query.Where(e => e.NotebookId == filter.NotebookId.Value);

        // Search
        if (!string.IsNullOrEmpty(filter.SearchQuery))
        {
            var searchLower = filter.SearchQuery.ToLowerInvariant();
            query = query.Where(e =>
                (e.Detail != null && e.Detail.Value.ToString().Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ||
                (e.Action?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Sorting
        query = filter.SortBy switch
        {
            "action" => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.Action)
                : query.OrderByDescending(e => e.Action),
            "actor" => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.AuthorId)
                : query.OrderByDescending(e => e.AuthorId),
            "target" => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.TargetType).ThenBy(e => e.TargetId)
                : query.OrderByDescending(e => e.TargetType).ThenByDescending(e => e.TargetId),
            _ => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.Timestamp)
                : query.OrderByDescending(e => e.Timestamp)
        };

        return query;
    }

    private AuditStats CalculateStats(List<AuditLogEntryDto> entries)
    {
        if (entries.Count == 0)
            return new AuditStats
            {
                TotalActions = 0,
                SuccessCount = 0,
                FailureCount = 0,
                UniqueActors = 0
            };

        return new AuditStats
        {
            TotalActions = entries.Count,
            SuccessCount = entries.Count, // Assuming all are successful for now
            FailureCount = 0,
            UniqueActors = entries.Select(e => e.AuthorId).Distinct().Count(),
            MostCommonAction = entries
                .GroupBy(e => e.Action)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key,
            EarliestEntry = entries.Min(e => e.Timestamp),
            LatestEntry = entries.Max(e => e.Timestamp)
        };
    }

    private static string CsvEncodeLine(string[] fields)
    {
        var encoded = fields.Select(field =>
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                var escaped = field.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }
            return field;
        });
        return string.Join(",", encoded);
    }
}
