# Admin Panel Phase 4: Advanced Audit Filtering and Reporting

**Depends on:** Phase 0 (Admin Shell), Phase 1 (User Management with Audit Trail)

**Estimated Effort:** 7-9 hours

## Context

Phase 4 enhances the audit trail functionality with advanced filtering, analytics, and reporting capabilities. Currently, the Audit page shows activity but lacks sophisticated filtering and export options needed for compliance, security investigation, and operational reporting.

**Motivation:** System administrators need to:
- Investigate specific events (security incidents, user actions)
- Generate compliance reports for auditors
- Track specific users' activities over time
- Analyze action patterns and trends
- Export audit logs for external systems
- Identify suspicious activity quickly

## Goal

Implement comprehensive audit filtering, analytics, and reporting:
1. **Advanced Filters** — Date ranges, action types, users, targets, status
2. **Analytics Dashboard** — Activity trends, action distribution, user rankings
3. **Export Functionality** — CSV and JSON export with selected filters
4. **Search** — Full-text search across audit log content
5. **Saved Filters** — Save and reuse common filter combinations
6. **Real-time Stats** — Quick counts and summaries of filtered results

## 4.1 — Enhanced Filter Model

### FilterModel

**File:** `frontend/admin/Models/AuditFilterModel.cs` (new file)

```csharp
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
    public Guid? TargetId { get; set; }

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
        TargetId.HasValue || NotebookId.HasValue ||
        !string.IsNullOrEmpty(Status) || !string.IsNullOrEmpty(SearchQuery) ||
        MinSeverity.HasValue;
}
```

### FilterResult

```csharp
namespace NotebookAdmin.Models;

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
```

## 4.2 — Audit Service Extensions

### Enhanced AuditService

**File:** `frontend/admin/Services/AuditService.cs` (extend existing)

```csharp
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
            // Build query parameters
            var queryParams = BuildQueryParameters(filter);

            // Call backend API
            var response = await _apiClient.QueryGlobalAuditAsync(
                adminAuthorId,
                actor: filter.ActorId,
                action: filter.Action,
                limit: filter.PageSize + 1, // Get one extra to determine if more exist
                offset: filter.PageNumber * filter.PageSize);

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
            "status",
            "details"
        }));

        // Data rows
        foreach (var entry in result.Entries)
        {
            sb.AppendLine(CsvEncodeLine(new[]
            {
                entry.Timestamp.ToString("O"),
                entry.ActorId ?? "",
                entry.Action ?? "",
                entry.TargetType ?? "",
                entry.TargetId?.ToString() ?? "",
                entry.NotebookId?.ToString() ?? "",
                "success",
                entry.Details ?? ""
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

        // Target filters
        if (filter.TargetType != null)
            query = query.Where(e => e.TargetType == filter.TargetType);
        if (filter.TargetId.HasValue)
            query = query.Where(e => e.TargetId == filter.TargetId.Value);
        if (filter.NotebookId.HasValue)
            query = query.Where(e => e.NotebookId == filter.NotebookId.Value);

        // Search
        if (!string.IsNullOrEmpty(filter.SearchQuery))
        {
            var searchLower = filter.SearchQuery.ToLowerInvariant();
            query = query.Where(e =>
                (e.Details?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Action?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Sorting
        query = filter.SortBy switch
        {
            "action" => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.Action)
                : query.OrderByDescending(e => e.Action),
            "actor" => filter.SortDirection == "asc"
                ? query.OrderBy(e => e.ActorId)
                : query.OrderByDescending(e => e.ActorId),
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
            UniqueActors = entries.Select(e => e.ActorId).Distinct().Count(),
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
```

## 4.3 — Audit Filter Page

### Enhanced Audit Page

**File:** `frontend/admin/Components/Pages/Admin/Audit.razor` (enhance existing)

Add filter panel and enhanced UI:

```razor
@page "/admin/audit"
@layout AdminLayout
@attribute [Authorize]
@using NotebookAdmin.Models
@using NotebookAdmin.Services
@inject AuditService AuditService
@inject NavigationManager Navigation
@inject IJSRuntime JS
@rendermode InteractiveServer

<PageTitle>Audit Log</PageTitle>

<h3>Audit Log & Reporting</h3>

@if (successMessage != null)
{
    <div class="alert alert-success alert-dismissible">
        @successMessage
        <button type="button" class="btn-close" @onclick="() => successMessage = null"></button>
    </div>
}

@if (errorMessage != null)
{
    <div class="alert alert-danger alert-dismissible">
        @errorMessage
        <button type="button" class="btn-close" @onclick="() => errorMessage = null"></button>
    </div>
}

<!-- Filter Panel -->
<div class="card mb-3">
    <div class="card-header">
        <span>Advanced Filters</span>
        <button class="btn btn-sm btn-outline-secondary float-end" @onclick="ToggleFilters">
            @(showFilters ? "Hide" : "Show")
        </button>
    </div>

    @if (showFilters)
    {
        <div class="card-body">
            <div class="row g-3">
                <!-- Date Range -->
                <div class="col-md-2">
                    <label class="form-label">From Date</label>
                    <input type="date" class="form-control" @bind="filter.DateFrom" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">To Date</label>
                    <input type="date" class="form-control" @bind="filter.DateTo" />
                </div>

                <!-- User/Actor -->
                <div class="col-md-3">
                    <label class="form-label">Actor (User)</label>
                    <input type="text" class="form-control" placeholder="Username or ID"
                           @bind="filter.ActorUsername" />
                </div>

                <!-- Action Type -->
                <div class="col-md-2">
                    <label class="form-label">Action</label>
                    <select class="form-select" @bind="filter.Action">
                        <option value="">All Actions</option>
                        <option value="Create">Create</option>
                        <option value="Update">Update</option>
                        <option value="Delete">Delete</option>
                        <option value="Lock">Lock</option>
                        <option value="Unlock">Unlock</option>
                        <option value="Login">Login</option>
                        <option value="Export">Export</option>
                        <option value="Import">Import</option>
                    </select>
                </div>

                <!-- Target Type -->
                <div class="col-md-2">
                    <label class="form-label">Target Type</label>
                    <select class="form-select" @bind="filter.TargetType">
                        <option value="">All Types</option>
                        <option value="User">User</option>
                        <option value="Notebook">Notebook</option>
                        <option value="Entry">Entry</option>
                        <option value="Organization">Organization</option>
                    </select>
                </div>

                <!-- Search -->
                <div class="col-md-3">
                    <label class="form-label">Search Details</label>
                    <input type="text" class="form-control" placeholder="Search in details..."
                           @bind="filter.SearchQuery" />
                </div>

                <!-- Page Size -->
                <div class="col-md-2">
                    <label class="form-label">Results Per Page</label>
                    <select class="form-select" @bind="filter.PageSize">
                        <option value="25">25</option>
                        <option value="50">50</option>
                        <option value="100">100</option>
                        <option value="250">250</option>
                    </select>
                </div>

                <!-- Sort -->
                <div class="col-md-2">
                    <label class="form-label">Sort By</label>
                    <select class="form-select" @bind="filter.SortBy">
                        <option value="timestamp">Date</option>
                        <option value="action">Action</option>
                        <option value="actor">Actor</option>
                        <option value="target">Target</option>
                    </select>
                </div>

                <!-- Buttons -->
                <div class="col-12 mt-2">
                    <button class="btn btn-primary" @onclick="ApplyFilters" disabled="@isLoading">
                        @(isLoading ? "Loading..." : "Apply Filters")
                    </button>
                    <button class="btn btn-outline-secondary" @onclick="ResetFilters">
                        Reset
                    </button>
                    <button class="btn btn-outline-info" @onclick="ExportCsv" disabled="@isLoading">
                        Export CSV
                    </button>
                    <button class="btn btn-outline-info" @onclick="ExportJson" disabled="@isLoading">
                        Export JSON
                    </button>
                </div>
            </div>
        </div>
    }
</div>

<!-- Stats Panel -->
@if (result != null)
{
    <div class="row mb-3">
        <div class="col-md-2">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="text-muted">Total Actions</h5>
                    <h3>@result.Stats.TotalActions</h3>
                </div>
            </div>
        </div>
        <div class="col-md-2">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="text-muted">Unique Actors</h5>
                    <h3>@result.Stats.UniqueActors</h3>
                </div>
            </div>
        </div>
        <div class="col-md-2">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="text-muted">Success Rate</h5>
                    <h3>@(result.Stats.TotalActions > 0 ? ((result.Stats.SuccessCount * 100) / result.Stats.TotalActions) : 0)%</h3>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="text-muted">Most Common Action</h5>
                    <h3>@(result.Stats.MostCommonAction ?? "-")</h3>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="text-muted">Date Range</h5>
                    <small>@(result.Stats.EarliestEntry?.ToString("yyyy-MM-dd") ?? "-") to @(result.Stats.LatestEntry?.ToString("yyyy-MM-dd") ?? "-")</small>
                </div>
            </div>
        </div>
    </div>
}

<!-- Results Table -->
@if (result == null)
{
    <p class="text-muted">Load audit logs with filters above.</p>
}
else if (result.Entries.Count == 0)
{
    <div class="alert alert-info">No entries found matching the filters.</div>
}
else
{
    <div class="table-responsive">
        <table class="table table-striped">
            <thead>
                <tr>
                    <th>Timestamp</th>
                    <th>Actor</th>
                    <th>Action</th>
                    <th>Target</th>
                    <th>Details</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var entry in result.Entries)
                {
                    <tr>
                        <td class="text-muted small">@entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")</td>
                        <td><small>@(entry.ActorId ?? "-")</small></td>
                        <td><span class="badge bg-info">@(entry.Action ?? "-")</span></td>
                        <td><small>@(entry.TargetType ?? "-"): @(entry.TargetId?.ToString()[..8] ?? "-")</small></td>
                        <td><small class="text-muted">@(entry.Details ?? "-")</small></td>
                    </tr>
                }
            </tbody>
        </table>
    </div>

    <!-- Pagination -->
    @if (result.TotalPages > 1)
    {
        <nav aria-label="Page navigation">
            <ul class="pagination">
                <li class="page-item" disabled="@(filter.PageNumber == 0)">
                    <button class="page-link" @onclick="() => GoToPage(filter.PageNumber - 1)">Previous</button>
                </li>
                @for (int i = 0; i < result.TotalPages; i++)
                {
                    var pageNum = i;
                    <li class="page-item" disabled="@(pageNum == filter.PageNumber)">
                        <button class="page-link" @onclick="() => GoToPage(pageNum)">@(pageNum + 1)</button>
                    </li>
                }
                <li class="page-item" disabled="@(filter.PageNumber >= result.TotalPages - 1)">
                    <button class="page-link" @onclick="() => GoToPage(filter.PageNumber + 1)">Next</button>
                </li>
            </ul>
        </nav>
        <p class="text-muted text-center">
            Page @(result.CurrentPage + 1) of @result.TotalPages
            (@result.Entries.Count of @result.TotalCount results shown)
        </p>
    }
}

@code {
    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    private AuditFilterModel filter = new();
    private AuditFilterResult? result;
    private bool showFilters = true;
    private bool isLoading = false;
    private string? successMessage;
    private string? errorMessage;
    private string? adminAuthorId;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState != null)
        {
            var authState = await AuthState;
            // Get admin's author ID (from claims or service)
            adminAuthorId = authState.User.FindFirst("author_id")?.Value;
        }
    }

    private async Task ApplyFilters()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            if (adminAuthorId == null) return;
            filter.PageNumber = 0; // Reset to page 1 when applying new filters
            result = await AuditService.GetFilteredAuditAsync(adminAuthorId, filter);
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading audit logs: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ExportCsv()
    {
        isLoading = true;
        try
        {
            if (adminAuthorId == null) return;
            var csv = await AuditService.ExportAuditCsvAsync(adminAuthorId, filter);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
            await JS.InvokeVoidAsync("triggerFileDownload", $"audit_export_{timestamp}.csv", csv);
            successMessage = "Audit log exported to CSV";
        }
        catch (Exception ex)
        {
            errorMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ExportJson()
    {
        isLoading = true;
        try
        {
            if (adminAuthorId == null) return;
            var json = await AuditService.ExportAuditJsonAsync(adminAuthorId, filter);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
            await JS.InvokeVoidAsync("triggerFileDownload", $"audit_export_{timestamp}.json", json);
            successMessage = "Audit log exported to JSON";
        }
        catch (Exception ex)
        {
            errorMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ToggleFilters() => showFilters = !showFilters;

    private void ResetFilters()
    {
        filter = new();
        result = null;
    }

    private async Task GoToPage(int pageNum)
    {
        if (pageNum < 0 || pageNum >= (result?.TotalPages ?? 1)) return;
        filter.PageNumber = pageNum;
        await ApplyFilters();
    }
}
```

## 4.4 — Saved Filters (Optional Enhancement)

### SavedAuditFilter Model

```csharp
namespace NotebookAdmin.Models;

public class SavedAuditFilter
{
    public int Id { get; set; }

    /// <summary>
    /// User ID who created this filter.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the filter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Filter configuration serialized as JSON.
    /// </summary>
    public string FilterJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
```

**Add to DbContext:**

```csharp
public DbSet<SavedAuditFilter> SavedAuditFilters => Set<SavedAuditFilter>();
```

**Add to Audit page:**

```csharp
private async Task SaveCurrentFilter(string name)
{
    // Serialize filter to JSON
    var filterJson = JsonSerializer.Serialize(filter);
    var saved = new SavedAuditFilter
    {
        UserId = userId,
        Name = name,
        FilterJson = filterJson
    };
    // Save to database
}

private async Task LoadSavedFilter(SavedAuditFilter saved)
{
    filter = JsonSerializer.Deserialize<AuditFilterModel>(saved.FilterJson) ?? new();
    await ApplyFilters();
}
```

## 4.5 — Migration for SavedFilters (Optional)

**File:** `infrastructure/postgres/migrations/admin/024_saved_audit_filters.sql`

```sql
CREATE TABLE admin."SavedAuditFilters" (
    "Id" serial PRIMARY KEY,
    "UserId" text NOT NULL,
    "Name" text NOT NULL,
    "FilterJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT fk_saved_filters_user FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE
);

CREATE INDEX idx_saved_filters_user ON admin."SavedAuditFilters"("UserId");
CREATE INDEX idx_saved_filters_name ON admin."SavedAuditFilters"("Name");
```

## 4.6 — Testing

### Unit Tests: Audit Service

```csharp
[Fact]
public async Task GetFilteredAuditAsync_DateRange_FiltersCorrectly()
{
    var filter = new AuditFilterModel
    {
        DateFrom = DateTime.UtcNow.AddDays(-1),
        DateTo = DateTime.UtcNow
    };

    var result = await _auditService.GetFilteredAuditAsync("admin-id", filter);

    Assert.NotNull(result);
    Assert.All(result.Entries, e =>
        Assert.True(e.Timestamp >= filter.DateFrom && e.Timestamp <= filter.DateTo));
}

[Fact]
public async Task GetFilteredAuditAsync_ActionFilter_FiltersCorrectly()
{
    var filter = new AuditFilterModel { Action = "Create" };
    var result = await _auditService.GetFilteredAuditAsync("admin-id", filter);

    Assert.All(result.Entries, e => Assert.Equal("Create", e.Action));
}

[Fact]
public async Task ExportAuditCsvAsync_ValidFilter_GeneratesCsv()
{
    var filter = new AuditFilterModel();
    var csv = await _auditService.ExportAuditCsvAsync("admin-id", filter);

    Assert.NotNull(csv);
    Assert.Contains("timestamp", csv);
    Assert.Contains("action", csv);
}
```

### Integration Tests: Audit Page

1. **Filter Application:**
   - Apply date range filter
   - Verify results update
   - Verify pagination works

2. **Stats Display:**
   - Verify stats show correct counts
   - Verify date range displayed
   - Verify most common action calculated

3. **Export:**
   - Export as CSV
   - Verify file downloads
   - Verify CSV format correct
   - Export as JSON
   - Verify JSON valid

4. **Pagination:**
   - Navigate pages
   - Verify correct entries shown
   - Verify page numbers correct

## 4.7 — Verification Checklist

- [ ] Build succeeds: `dotnet build`
- [ ] Audit page loads: `/admin/audit`
- [ ] Filter panel shows all controls
- [ ] Date range filter works
- [ ] Actor/User filter works
- [ ] Action type filter works
- [ ] Target type filter works
- [ ] Search in details works
- [ ] Stats calculate correctly
- [ ] Pagination works
- [ ] Export CSV works
- [ ] Export JSON works
- [ ] CSV format correct
- [ ] JSON format valid
- [ ] Page size selector changes results
- [ ] Sort options work
- [ ] Reset filters clears all
- [ ] Unit tests pass
- [ ] Integration tests pass

## Implementation Order

1. **Part 1** — Create AuditFilterModel and AuditFilterResult (20 min)
2. **Part 2** — Extend AuditService with filtering and export (60 min)
3. **Part 3** — Enhance Audit.razor page with UI (90 min)
4. **Part 4** — Add saved filters (optional, 45 min)
5. **Part 5** — Create migration for saved filters (optional, 15 min)
6. **Testing** — Unit and integration tests (60 min)

## Deliverables

- ✅ AuditFilterModel with comprehensive filter options
- ✅ Enhanced AuditService with filtering and export
- ✅ Advanced Audit page with filter panel, stats, pagination
- ✅ CSV export functionality
- ✅ JSON export functionality
- ✅ Pagination support
- ✅ Search in audit details
- ✅ Stats dashboard
- ✅ Optional: Saved filters UI and storage
- ✅ Unit and integration tests
- ✅ Documentation

## Key Features

**Filtering:**
- Date range (from/to)
- Actor/User
- Action type (Create, Update, Delete, Lock, etc.)
- Target type (User, Notebook, Entry, Organization)
- Full-text search in details
- Status filter

**Analytics:**
- Total actions count
- Unique actors count
- Success rate
- Most common action
- Date range of results
- Real-time statistics

**Export:**
- CSV with all fields
- JSON with full structure
- Applies current filters
- Timestamp-based filenames
- Browser download

**UX:**
- Collapsible filter panel
- Real-time stats dashboard
- Pagination for large result sets
- Multiple sort options
- Page size selector
- Reset filters button

**Performance:**
- Client-side filtering for fast response
- Pagination to limit results
- Lazy stats calculation
- Efficient CSV/JSON generation

---

**Status:** 📋 PLAN READY
**Estimated Effort:** 7-9 hours
**Complexity:** Medium
**Risk:** Low (no schema changes for core feature, optional saved filters)
