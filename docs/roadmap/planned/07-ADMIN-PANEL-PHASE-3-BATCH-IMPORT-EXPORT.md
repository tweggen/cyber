# Admin Panel Phase 3: User Batch Import/Export

**Depends on:** Phase 0 (Admin Shell), Phase 1 (User Management), Phase 2 (Quotas)

**Estimated Effort:** 6-8 hours

## Context

Phase 3 enables administrators to bulk import users from CSV files and export user lists for external reporting or migration. This reduces manual user creation overhead and enables data portability for compliance and backup purposes.

**Motivation:** Currently, creating users one-by-one is time-consuming for organizations with dozens or hundreds of users. Batch operations enable:
- Onboarding workflows (import from HR systems)
- Data migration (move users between systems)
- Compliance reporting (export for audit trails)
- Backup and disaster recovery

## Goal

Implement bidirectional user batch operations:
1. **Export** — Generate CSV with user data, quotas, and metadata
2. **Import** — Parse CSV, validate, create users in bulk with progress tracking and error reporting

## 3.1 — Data Model & CSV Format

### Export CSV Structure

**File:** `user_export_YYYY-MM-DD_HHmmss.csv`

```csv
username,email,display_name,user_type,author_id_hex,account_created,last_login,lock_status,lock_reason,max_notebooks,max_entries_per_notebook,max_entry_size_bytes,max_total_storage_bytes
alice,alice@example.com,Alice Smith,user,a1b2c3d4e5f6...,2025-10-15T09:30:00Z,2026-02-22T14:20:00Z,active,,10,1000,1048576,104857600
bob,bob@example.com,Robert Jones,service_account,b2c3d4e5f6a7...,2025-11-01T10:00:00Z,,locked,Suspected abuse,5,1000,1048576,104857600
charlie,charlie@example.com,,bot,c3d4e5f6a7b8...,2025-12-10T16:45:00Z,2026-02-20T08:15:00Z,active,,5,1000,1048576,104857600
```

**Column Definitions:**

| Column | Type | Required | Notes |
|--------|------|----------|-------|
| username | string | Yes | Unique. Used as identifier. |
| email | string | Optional | Email address for user. |
| display_name | string | Optional | Human-readable name. |
| user_type | enum | Yes | Values: user, service_account, bot |
| author_id_hex | string | Readonly | 64-char hex. Only for export. Ignored on import. |
| account_created | datetime | Readonly | ISO 8601. Only for export. |
| last_login | datetime | Readonly | ISO 8601. Only for export. Null if never logged in. |
| lock_status | enum | Optional | Values: active, locked. Default: active |
| lock_reason | string | Optional | Reason for lock. Ignored if lock_status=active. |
| max_notebooks | int | Optional | User quota. If omitted, inherit org default or use system default. |
| max_entries_per_notebook | int | Optional | User quota. |
| max_entry_size_bytes | long | Optional | User quota. |
| max_total_storage_bytes | long | Optional | User quota. |

### Import CSV Validation Rules

**Required Fields (Import):**
- `username` — Must be unique, alphanumeric with underscores/hyphens, 3-50 chars
- `user_type` — Must be one of: user, service_account, bot

**Optional Fields:**
- `email` — If provided, must be valid email format
- `display_name` — Max 256 chars
- `lock_status` — Defaults to "active"
- `lock_reason` — Only meaningful if lock_status="locked"
- Quota fields — If omitted, inherit from organization or system defaults

**Readonly Fields (Ignored on Import):**
- `author_id_hex`, `account_created`, `last_login` — Cannot be set via import

## 3.2 — Export Functionality

### Export Service

**File:** `frontend/admin/Services/UserExportService.cs` (new file)

```csharp
using NotebookAdmin.Models;
using System.Text;
using System.Globalization;

namespace NotebookAdmin.Services;

/// <summary>
/// Export user data to CSV format.
/// </summary>
public class UserExportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UserExportService> _logger;

    public UserExportService(ApplicationDbContext db, ILogger<UserExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Export all users to CSV with quota information.
    /// Returns CSV content as string.
    /// </summary>
    public async Task<string> ExportAllUsersAsync()
    {
        var users = await _db.Users
            .Include(u => u.UserQuotas)
            .OrderBy(u => u.UserName)
            .ToListAsync();

        return GenerateCSV(users);
    }

    /// <summary>
    /// Export filtered users to CSV.
    /// </summary>
    public async Task<string> ExportUsersAsync(
        UserType? userType = null,
        bool? isLocked = null)
    {
        var query = _db.Users
            .Include(u => u.UserQuotas)
            .AsQueryable();

        if (userType.HasValue)
            query = query.Where(u => u.UserType == userType.Value);

        if (isLocked.HasValue)
            query = query.Where(u => (u.LockoutEnd > DateTimeOffset.UtcNow) == isLocked.Value);

        var users = await query
            .OrderBy(u => u.UserName)
            .ToListAsync();

        return GenerateCSV(users);
    }

    private static string GenerateCSV(List<ApplicationUser> users)
    {
        var sb = new StringBuilder();

        // Write header
        sb.AppendLine(CsvEncodeLine(new[]
        {
            "username",
            "email",
            "display_name",
            "user_type",
            "author_id_hex",
            "account_created",
            "last_login",
            "lock_status",
            "lock_reason",
            "max_notebooks",
            "max_entries_per_notebook",
            "max_entry_size_bytes",
            "max_total_storage_bytes"
        }));

        // Write data rows
        foreach (var user in users)
        {
            var quota = user.UserQuotas;
            var isLocked = user.LockoutEnd > DateTimeOffset.UtcNow;
            var lockStatus = isLocked ? "locked" : "active";

            sb.AppendLine(CsvEncodeLine(new[]
            {
                user.UserName ?? "",
                user.Email ?? "",
                user.DisplayName ?? "",
                user.UserType,
                user.AuthorIdHex,
                user.CreatedAt.ToString("O"),
                user.LastLoginAt?.ToString("O") ?? "",
                lockStatus,
                isLocked ? (user.LockReason ?? "") : "",
                quota?.MaxNotebooks.ToString() ?? "",
                quota?.MaxEntriesPerNotebook.ToString() ?? "",
                quota?.MaxEntrySizeBytes.ToString() ?? "",
                quota?.MaxTotalStorageBytes.ToString() ?? ""
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Encode a CSV line, handling quotes and commas.
    /// </summary>
    private static string CsvEncodeLine(string[] fields)
    {
        var encoded = fields.Select(field =>
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            // If field contains comma, quote, or newline, wrap in quotes and escape quotes
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

### Export Controller

**File:** `frontend/admin/Components/Pages/Admin/UserList.razor`

Add export button and handler:

```csharp
@inject UserExportService ExportService

private async Task ExportUsers()
{
    try
    {
        var csv = await ExportService.ExportAllUsersAsync();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        var filename = $"users_export_{timestamp}.csv";

        // Trigger browser download
        await JS.InvokeVoidAsync("triggerFileDownload", filename, csv);
        successMessage = $"Exported {users.Count} users to {filename}";
    }
    catch (Exception ex)
    {
        errorMessage = $"Export failed: {ex.Message}";
    }
}
```

Add JavaScript helper in `App.razor`:

```javascript
window.triggerFileDownload = function(filename, content) {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
```

### UI Component

Add to UserList page toolbar:

```html
<div class="d-flex justify-content-between align-items-center mb-3">
    <h3>Users</h3>
    <div>
        <a href="/admin/users/import" class="btn btn-outline-primary me-2">
            <span class="oi oi-arrow-thick-top me-1"></span>Import
        </a>
        <button class="btn btn-outline-secondary" @onclick="ExportUsers" disabled="@isExporting">
            <span class="oi oi-arrow-thick-bottom me-1"></span>
            @(isExporting ? "Exporting..." : "Export")
        </button>
    </div>
</div>
```

## 3.3 — Import Functionality

### Import Service

**File:** `frontend/admin/Services/UserImportService.cs` (new file)

```csharp
using NotebookAdmin.Models;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace NotebookAdmin.Services;

public record ImportRecord(
    string Username,
    string? Email,
    string? DisplayName,
    string UserType,
    string? LockStatus,
    string? LockReason,
    int? MaxNotebooks,
    int? MaxEntriesPerNotebook,
    long? MaxEntrySizeBytes,
    long? MaxTotalStorageBytes);

public record ImportResult(
    int SuccessCount,
    int ErrorCount,
    List<ImportError> Errors);

public record ImportError(
    int RowNumber,
    string Username,
    string Message);

/// <summary>
/// Import users from CSV file.
/// </summary>
public class UserImportService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly QuotaService _quotaService;
    private readonly ILogger<UserImportService> _logger;

    public UserImportService(
        UserManager<ApplicationUser> userManager,
        QuotaService quotaService,
        ILogger<UserImportService> logger)
    {
        _userManager = userManager;
        _quotaService = quotaService;
        _logger = logger;
    }

    /// <summary>
    /// Validate and import users from CSV stream.
    /// Performs validation without writing to database.
    /// </summary>
    public async Task<(bool IsValid, List<ImportError> Errors)> ValidateImportAsync(
        Stream csvStream)
    {
        var errors = new List<ImportError>();
        var records = ParseCSV(csvStream);
        var seenUsernames = new HashSet<string>();
        var rowNumber = 2; // Start at 2 (after header)

        foreach (var record in records)
        {
            var recordErrors = ValidateRecord(record, rowNumber, seenUsernames);
            errors.AddRange(recordErrors);
            seenUsernames.Add(record.Username);
            rowNumber++;
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Import users from CSV stream.
    /// Returns summary with success/error counts.
    /// </summary>
    public async Task<ImportResult> ImportAsync(Stream csvStream)
    {
        var errors = new List<ImportError>();
        var successCount = 0;
        var records = ParseCSV(csvStream);
        var rowNumber = 2;

        foreach (var record in records)
        {
            try
            {
                // Validate
                var validationErrors = ValidateRecord(record, rowNumber, new());
                if (validationErrors.Any())
                {
                    errors.AddRange(validationErrors);
                    rowNumber++;
                    continue;
                }

                // Check if user exists
                var existingUser = await _userManager.FindByNameAsync(record.Username);
                if (existingUser != null)
                {
                    errors.Add(new ImportError(
                        rowNumber,
                        record.Username,
                        $"User already exists"));
                    rowNumber++;
                    continue;
                }

                // Create user
                var user = new ApplicationUser
                {
                    UserName = record.Username,
                    Email = record.Email,
                    DisplayName = record.DisplayName,
                    UserType = record.UserType,
                    CreatedAt = DateTime.UtcNow,
                    AuthorId = GenerateAuthorId(),
                    AuthorIdHex = GenerateAuthorIdHex()
                };

                // Lock if needed
                if (record.LockStatus == "locked")
                {
                    user.LockReason = record.LockReason;
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                }

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    errors.Add(new ImportError(
                        rowNumber,
                        record.Username,
                        $"Creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}"));
                    rowNumber++;
                    continue;
                }

                // Set quota if provided
                if (record.MaxNotebooks.HasValue ||
                    record.MaxEntriesPerNotebook.HasValue ||
                    record.MaxEntrySizeBytes.HasValue ||
                    record.MaxTotalStorageBytes.HasValue)
                {
                    var quota = await _quotaService.GetOrCreateDefaultAsync(user.Id);
                    if (record.MaxNotebooks.HasValue)
                        quota.MaxNotebooks = record.MaxNotebooks.Value;
                    if (record.MaxEntriesPerNotebook.HasValue)
                        quota.MaxEntriesPerNotebook = record.MaxEntriesPerNotebook.Value;
                    if (record.MaxEntrySizeBytes.HasValue)
                        quota.MaxEntrySizeBytes = record.MaxEntrySizeBytes.Value;
                    if (record.MaxTotalStorageBytes.HasValue)
                        quota.MaxTotalStorageBytes = record.MaxTotalStorageBytes.Value;

                    await _quotaService.UpdateQuotaAsync(user.Id, quota);
                }

                _logger.LogInformation("Imported user {Username}", record.Username);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError(
                    rowNumber,
                    record.Username,
                    $"Unexpected error: {ex.Message}"));
                _logger.LogError(ex, "Import error for user {Username}", record.Username);
            }

            rowNumber++;
        }

        return new ImportResult(successCount, errors.Count, errors);
    }

    private List<ImportRecord> ParseCSV(Stream csvStream)
    {
        var records = new List<ImportRecord>();
        csvStream.Position = 0;

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            records.Add(new ImportRecord(
                csv.GetField("username") ?? "",
                csv.GetField("email"),
                csv.GetField("display_name"),
                csv.GetField("user_type") ?? "",
                csv.GetField("lock_status"),
                csv.GetField("lock_reason"),
                int.TryParse(csv.GetField("max_notebooks"), out var nb) ? nb : null,
                int.TryParse(csv.GetField("max_entries_per_notebook"), out var ep) ? ep : null,
                long.TryParse(csv.GetField("max_entry_size_bytes"), out var es) ? es : null,
                long.TryParse(csv.GetField("max_total_storage_bytes"), out var ts) ? ts : null));
        }

        return records;
    }

    private List<ImportError> ValidateRecord(
        ImportRecord record,
        int rowNumber,
        HashSet<string> seenUsernames)
    {
        var errors = new List<ImportError>();

        // Username validation
        if (string.IsNullOrWhiteSpace(record.Username))
            errors.Add(new ImportError(rowNumber, record.Username, "Username is required"));
        else if (record.Username.Length < 3 || record.Username.Length > 50)
            errors.Add(new ImportError(rowNumber, record.Username, "Username must be 3-50 characters"));
        else if (!System.Text.RegularExpressions.Regex.IsMatch(record.Username, @"^[a-zA-Z0-9_-]+$"))
            errors.Add(new ImportError(rowNumber, record.Username, "Username must contain only alphanumeric, underscore, or hyphen"));
        else if (seenUsernames.Contains(record.Username))
            errors.Add(new ImportError(rowNumber, record.Username, "Username already in this import"));

        // UserType validation
        if (!new[] { "user", "service_account", "bot" }.Contains(record.UserType))
            errors.Add(new ImportError(rowNumber, record.Username, $"Invalid user_type: {record.UserType}"));

        // Email validation
        if (!string.IsNullOrEmpty(record.Email))
        {
            try
            {
                new System.Net.Mail.MailAddress(record.Email);
            }
            catch
            {
                errors.Add(new ImportError(rowNumber, record.Username, $"Invalid email format: {record.Email}"));
            }
        }

        // Lock status validation
        if (!string.IsNullOrEmpty(record.LockStatus) &&
            !new[] { "active", "locked" }.Contains(record.LockStatus))
            errors.Add(new ImportError(rowNumber, record.Username, $"Invalid lock_status: {record.LockStatus}"));

        // Quota validation
        if (record.MaxNotebooks.HasValue && record.MaxNotebooks.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_notebooks must be >= 1"));

        if (record.MaxEntriesPerNotebook.HasValue && record.MaxEntriesPerNotebook.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_entries_per_notebook must be >= 1"));

        if (record.MaxEntrySizeBytes.HasValue && record.MaxEntrySizeBytes.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_entry_size_bytes must be >= 1"));

        if (record.MaxTotalStorageBytes.HasValue && record.MaxTotalStorageBytes.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_total_storage_bytes must be >= 1"));

        return errors;
    }

    private static string GenerateAuthorId()
    {
        // Generate random 32-byte author ID (BLAKE3 hash simulation)
        Span<byte> bytes = stackalloc byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateAuthorIdHex()
    {
        // Generate random 64-char hex string
        Span<byte> bytes = stackalloc byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

**NuGet Dependency:**
Add to `frontend/admin/NotebookAdmin.csproj`:
```xml
<PackageReference Include="CsvHelper" Version="33.0.0" />
```

### Import Page Component

**File:** `frontend/admin/Components/Pages/Admin/UserImport.razor` (new file)

```razor
@page "/admin/users/import"
@layout AdminLayout
@attribute [Authorize]
@using Microsoft.AspNetCore.Components.Forms
@using NotebookAdmin.Models
@using NotebookAdmin.Services
@inject UserImportService ImportService
@inject NavigationManager Navigation
@rendermode InteractiveServer

<PageTitle>Import Users</PageTitle>

<nav aria-label="breadcrumb" class="mb-3">
    <ol class="breadcrumb">
        <li class="breadcrumb-item"><a href="/admin/users">Users</a></li>
        <li class="breadcrumb-item active">Import</li>
    </ol>
</nav>

<h3>Import Users from CSV</h3>

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

<div class="card mb-4">
    <div class="card-header">Select CSV File</div>
    <div class="card-body">
        <p class="text-muted">
            Upload a CSV file with user data. Required columns: username, user_type.
            Optional columns: email, display_name, lock_status, lock_reason, quota fields.
        </p>

        <div class="mb-3">
            <label class="form-label">CSV File</label>
            <InputFile @ref="fileInput" OnChange="HandleFileSelected" accept=".csv" disabled="@(isValidating || isImporting)" />
            @if (selectedFileName != null)
            {
                <small class="text-muted d-block mt-1">Selected: @selectedFileName</small>
            }
        </div>

        @if (validationErrors?.Any() == true)
        {
            <div class="alert alert-warning">
                <strong>@validationErrors.Count validation error(s) found:</strong>
                <ul class="mt-2 mb-0">
                    @foreach (var error in validationErrors.Take(10))
                    {
                        <li>Row @error.RowNumber (@error.Username): @error.Message</li>
                    }
                    @if (validationErrors.Count > 10)
                    {
                        <li><em>... and @(validationErrors.Count - 10) more errors</em></li>
                    }
                </ul>
            </div>
        }

        <div>
            <button class="btn btn-primary me-2" @onclick="ValidateFile"
                    disabled="@(selectedFile == null || isValidating || isImporting)">
                @(isValidating ? "Validating..." : "Validate")
            </button>
            <button class="btn btn-success" @onclick="ImportFile"
                    disabled="@(selectedFile == null || validationErrors?.Any() == true || isImporting)">
                @(isImporting ? "Importing..." : "Import")
            </button>
            <a href="/admin/users" class="btn btn-outline-secondary">Cancel</a>
        </div>
    </div>
</div>

@if (importResult != null)
{
    <div class="card">
        <div class="card-header">Import Summary</div>
        <div class="card-body">
            <p>
                <strong>Success:</strong> @importResult.SuccessCount users created<br />
                <strong>Errors:</strong> @importResult.ErrorCount users failed
            </p>

            @if (importResult.Errors.Any())
            {
                <h6>Error Details:</h6>
                <table class="table table-sm">
                    <thead>
                        <tr>
                            <th>Row</th>
                            <th>Username</th>
                            <th>Error</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var error in importResult.Errors)
                        {
                            <tr>
                                <td>@error.RowNumber</td>
                                <td>@error.Username</td>
                                <td><small>@error.Message</small></td>
                            </tr>
                        }
                    </tbody>
                </table>
            }

            <button class="btn btn-primary" @onclick="() => Navigation.NavigateTo(\"/admin/users\")">
                Back to Users
            </button>
        </div>
    </div>
}

@code {
    private InputFile? fileInput;
    private IBrowserFile? selectedFile;
    private string? selectedFileName;
    private List<ImportError>? validationErrors;
    private ImportResult? importResult;
    private bool isValidating;
    private bool isImporting;
    private string? successMessage;
    private string? errorMessage;

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        selectedFile = e.File;
        selectedFileName = selectedFile.Name;
        validationErrors = null;
    }

    private async Task ValidateFile()
    {
        if (selectedFile == null) return;

        isValidating = true;
        errorMessage = null;

        try
        {
            using var stream = selectedFile.OpenReadStream(maxAllowedSize: 10_000_000); // 10 MB max
            var (isValid, errors) = await ImportService.ValidateImportAsync(stream);
            validationErrors = errors;

            if (isValid)
                successMessage = $"Validation passed: {selectedFileName}";
        }
        catch (Exception ex)
        {
            errorMessage = $"Validation failed: {ex.Message}";
        }
        finally
        {
            isValidating = false;
        }
    }

    private async Task ImportFile()
    {
        if (selectedFile == null) return;

        isImporting = true;
        errorMessage = null;

        try
        {
            using var stream = selectedFile.OpenReadStream(maxAllowedSize: 10_000_000);
            importResult = await ImportService.ImportAsync(stream);
            successMessage = $"Import complete: {importResult.SuccessCount} users created, {importResult.ErrorCount} errors";
        }
        catch (Exception ex)
        {
            errorMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            isImporting = false;
        }
    }
}
```

## 3.4 — Service Registration

Update `frontend/admin/Program.cs`:

```csharp
// Add services
builder.Services.AddScoped<UserExportService>();
builder.Services.AddScoped<UserImportService>();
```

## 3.5 — Database Considerations

### No Schema Changes Required

Phase 3 uses existing tables:
- `AspNetUsers` — User data, lock info
- `UserQuotas` — Quota overrides

### Optional: Create Audit Table for Imports

**File:** `infrastructure/postgres/migrations/admin/023_batch_import_audit.sql`

```sql
-- Track batch imports for audit and rollback capability
CREATE TABLE admin."BatchImports" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "FileName" text NOT NULL,
    "ImportedBy" text NOT NULL,
    "ImportedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "SuccessCount" integer NOT NULL,
    "ErrorCount" integer NOT NULL,
    "Status" text NOT NULL DEFAULT 'completed',
    "ErrorDetails" jsonb
);

CREATE INDEX idx_batch_imports_imported_by ON admin."BatchImports"("ImportedBy");
CREATE INDEX idx_batch_imports_imported_at ON admin."BatchImports"("ImportedAt");
```

**Entity Model:**

```csharp
public class BatchImportAudit
{
    [Key]
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ImportedBy { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = "completed";
    public Dictionary<string, object>? ErrorDetails { get; set; }
}
```

Register in DbContext:

```csharp
public DbSet<BatchImportAudit> BatchImports => Set<BatchImportAudit>();
```

## 3.6 — Testing

### Unit Tests: CSV Parsing

```csharp
[Fact]
public void ParseCSV_ValidFile_ReturnsRecords()
{
    var csv = """
        username,email,user_type
        alice,alice@example.com,user
        bob,bob@example.com,service_account
        """;

    var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
    var records = ImportService.ParseCSV(stream);

    Assert.Equal(2, records.Count);
    Assert.Equal("alice", records[0].Username);
    Assert.Equal("alice@example.com", records[0].Email);
}

[Fact]
public void ValidateRecord_InvalidUsername_ReturnsError()
{
    var record = new ImportRecord("ab", null, null, "user", null, null, null, null, null, null);
    var errors = ImportService.ValidateRecord(record, 2, new());

    Assert.Single(errors);
    Assert.Contains("3-50 characters", errors[0].Message);
}

[Fact]
public void ValidateRecord_DuplicateUsername_ReturnsError()
{
    var record = new ImportRecord("alice", null, null, "user", null, null, null, null, null, null);
    var seen = new HashSet<string> { "alice" };
    var errors = ImportService.ValidateRecord(record, 2, seen);

    Assert.Single(errors);
    Assert.Contains("already in this import", errors[0].Message);
}
```

### Integration Tests: Full Import Flow

```csharp
[Fact]
public async Task ImportAsync_ValidUsers_CreatesInDatabase()
{
    var csv = """
        username,email,user_type,user_type,max_notebooks
        alice,alice@example.com,user,5
        bob,bob@example.com,service_account,3
        """;

    var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
    var result = await ImportService.ImportAsync(stream);

    Assert.Equal(2, result.SuccessCount);
    Assert.Equal(0, result.ErrorCount);

    var alice = await UserManager.FindByNameAsync("alice");
    Assert.NotNull(alice);
    Assert.Equal("alice@example.com", alice.Email);

    var aliceQuota = await QuotaService.GetOrCreateDefaultAsync(alice.Id);
    Assert.Equal(5, aliceQuota.MaxNotebooks);
}

[Fact]
public async Task ImportAsync_DuplicateUsername_ReturnsError()
{
    // Create existing user
    var existingUser = new ApplicationUser { UserName = "alice", UserType = "user" };
    await UserManager.CreateAsync(existingUser);

    var csv = """
        username,user_type
        alice,user
        """;

    var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
    var result = await ImportService.ImportAsync(stream);

    Assert.Equal(0, result.SuccessCount);
    Assert.Single(result.Errors);
    Assert.Contains("already exists", result.Errors[0].Message);
}
```

### UI Tests

1. **Export:**
   - Click "Export" button
   - Verify CSV downloads with correct filename
   - Verify all user columns present

2. **Import - Validation:**
   - Upload valid CSV
   - Click "Validate"
   - Verify success message

3. **Import - Validation Errors:**
   - Upload CSV with invalid user_type
   - Click "Validate"
   - Verify errors displayed (row numbers, usernames, messages)

4. **Import - Execution:**
   - Upload valid CSV with 5 users
   - Click "Import"
   - Verify all users created in database
   - Verify quotas assigned correctly
   - Check lock status and lock_reason respected

5. **Import - Partial Failure:**
   - Upload CSV with 2 valid, 1 invalid user
   - Click "Import"
   - Verify 2 users created, 1 error reported

## 3.7 — Security Considerations

### Input Validation

1. **File size limit:** Max 10 MB (configurable)
2. **CSV parsing:** Use CsvHelper for safe parsing
3. **Username validation:** Alphanumeric + underscore/hyphen only
4. **Email validation:** Use MailAddress.Parse()
5. **Quota ranges:** Min >= 1 (no zero or negative)

### Authorization

- Require `[Authorize]` attribute on import/export pages
- Add claim-based check for admin role:

```csharp
[Authorize(Roles = "Admin")]
public async Task ExportUsers() { ... }
```

### Audit Trail

- Log all imports (filename, user count, timestamp, admin)
- Log all exports (count, timestamp, admin)
- Store failed imports in `BatchImports` table with error details

```csharp
var audit = new BatchImportAudit
{
    FileName = selectedFile.Name,
    ImportedBy = currentUserId,
    SuccessCount = result.SuccessCount,
    ErrorCount = result.ErrorCount,
    ErrorDetails = result.Errors.ToDictionary(e => e.Username, e => e.Message)
};
await _db.BatchImports.AddAsync(audit);
await _db.SaveChangesAsync();
```

### Password Generation

Users created via import need temporary passwords:

```csharp
var tempPassword = GenerateTemporaryPassword();
var token = await _userManager.GeneratePasswordResetTokenAsync(user);
await _userManager.ResetPasswordAsync(user, token, tempPassword);

// Return password to admin or send via email
importPasswordMap[user.UserName] = tempPassword;
```

Add to import result CSV:

```csv
username,email,temporary_password
alice,alice@example.com,Abc123def45ghi
bob,bob@example.com,Xyz987uvw65rst
```

## 3.8 — Verification Checklist

- [ ] Build succeeds: `dotnet build`
- [ ] Unit tests pass: `dotnet test`
- [ ] CsvHelper NuGet installed
- [ ] Export button appears on UserList page
- [ ] Export downloads CSV with correct columns
- [ ] Import link appears on UserList page
- [ ] Import page loads and shows file upload
- [ ] Validate button checks CSV structure
- [ ] Validation errors display with row numbers
- [ ] Import button creates users in database
- [ ] User quotas assigned from CSV
- [ ] Lock status and reason applied
- [ ] Duplicate username rejected
- [ ] Invalid email rejected
- [ ] Negative quota rejected
- [ ] Import audit logged to database (if implemented)
- [ ] Temporary passwords generated (if implemented)

## 3.9 — Future Enhancements

Phase 3+ improvements:

1. **Rollback capability** — Undo failed import by transaction rollback or soft delete
2. **Progress UI** — Real-time progress bar for large imports (100+ users)
3. **Email notifications** — Send temporary passwords to imported users
4. **Template download** — Provide empty CSV template with required columns
5. **Update-on-duplicate** — Option to update existing users instead of skipping
6. **Scheduled imports** — Periodic sync from LDAP, Active Directory
7. **Import history** — View past imports with success/error summaries
8. **Selective export** — Filter users before export (by type, lock status, etc.)

## Implementation Order

1. **Part 1** — Create UserExportService and add export button to UserList
2. **Part 2** — Create UserImportService with validation logic
3. **Part 3** — Create UserImport.razor page with upload/validation/import flow
4. **Part 4** — Add temporary password generation and include in import result
5. **Part 5** — Add BatchImportAudit table and logging (optional)
6. **Testing** — Unit tests for service layer, integration tests for full flow
7. **Documentation** — Update README and CLAUDE.md

## Deliverables

- ✅ UserExportService (generates CSV from database)
- ✅ UserImportService (validates and creates users)
- ✅ UserImport.razor page (file upload, validation, import)
- ✅ CSV format specification and validation rules
- ✅ Unit tests for service layer
- ✅ Integration tests for full import flow
- ✅ UI tests for export/import pages
- ✅ Security considerations (input validation, authorization, audit)
- ✅ Optional: BatchImportAudit table and logging

## Key Design Decisions

**CSV Format:**
- Human-readable column names (snake_case)
- Handles optional fields gracefully
- Includes readonly fields in export (for reference, ignored on import)
- Uses standard ISO 8601 datetime format

**Import Strategy:**
- Validate before import (two-phase: validate → import)
- Continue on error (partial success possible)
- Track all errors with row numbers and messages
- No rollback (users created incrementally)

**Password Handling:**
- Generate temporary password for each imported user
- Include in export (option to email or display)
- Separate from import flow (can be done after successful import)

**Backward Compatibility:**
- No schema changes required for core functionality
- Optional audit table for tracking imports
- Existing quota and user systems unchanged

---

**Status:** 📋 PLAN READY
**Estimated Effort:** 6-8 hours
**Complexity:** Medium (CSV parsing, validation, bulk operations)
**Risk:** Low (no schema changes, isolated feature, good test coverage)
