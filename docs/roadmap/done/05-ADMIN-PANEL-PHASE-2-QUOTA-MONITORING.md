# Admin Panel Phase 2: Quota Monitoring

**Depends on:** Phase 0 (Admin Shell), Phase 1 (User Management)

**Completion Date:** February 22, 2026

## Goal

Implement organization-level quota defaults with user quota inheritance. Enable administrators to set baseline resource limits per organization, which users inherit unless they have custom overrides. Add usage visualization to the quota management page to help admins make informed quota decisions.

## 2.1 — Organization Quota Model

### Entity Model

**File:** `frontend/admin/Models/OrganizationQuota.cs`

```csharp
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
```

**Default Values:**
- MaxNotebooks: 50 (10x user default of 5)
- MaxEntriesPerNotebook: 5000 (5x user default of 1000)
- MaxEntrySizeBytes: 10 MB (10x user default of 1 MB)
- MaxTotalStorageBytes: 1 GB (10x user default of 100 MB)

### Register in DbContext

**File:** `frontend/admin/Data/ApplicationDbContext.cs`

Add DbSet and configuration:

```csharp
public DbSet<OrganizationQuota> OrganizationQuotas => Set<OrganizationQuota>();

protected override void OnModelCreating(ModelBuilder builder)
{
    // ... existing configurations ...

    builder.Entity<OrganizationQuota>(entity =>
    {
        entity.HasKey(e => e.OrganizationId);
    });
}
```

**Key Design Decisions:**
- **No foreign key** — OrganizationId is an external reference to the backend database. No constraint needed since organizations live in `thinktank` DB, not `notebook_admin` DB.
- **Separate PK** — Each organization has exactly one quota record.

## 2.2 — Database Migration

### SQL Migration

**File:** `infrastructure/postgres/migrations/admin/022_admin_organization_quotas.sql`

```sql
-- Create OrganizationQuotas table for admin panel
-- Organization-level default resource quotas for notebook platform

CREATE TABLE admin."OrganizationQuotas" (
    "OrganizationId" uuid NOT NULL,
    "MaxNotebooks" integer NOT NULL,
    "MaxEntriesPerNotebook" integer NOT NULL,
    "MaxEntrySizeBytes" bigint NOT NULL,
    "MaxTotalStorageBytes" bigint NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OrganizationQuotas" PRIMARY KEY ("OrganizationId")
);
```

**Migration Path:**
1. For local development: Use EF Core to apply during development
2. For production: Apply SQL migration to isolated admin database

Apply with:
```bash
psql -U postgres -d notebook_admin -f infrastructure/postgres/migrations/admin/022_admin_organization_quotas.sql
```

## 2.3 — Quota Service Extensions

### New Methods

**File:** `frontend/admin/Services/QuotaService.cs`

Add three methods to manage organization quotas and implement inheritance:

```csharp
/// <summary>
/// Get the quota for an organization, creating a default one if it doesn't exist.
/// </summary>
public async Task<OrganizationQuota> GetOrCreateOrgDefaultAsync(Guid orgId)
{
    var quota = await _db.OrganizationQuotas.FindAsync(orgId);
    if (quota != null)
        return quota;

    quota = new OrganizationQuota { OrganizationId = orgId };
    _db.OrganizationQuotas.Add(quota);
    await _db.SaveChangesAsync();
    _logger.LogInformation("Created default quota for organization {OrgId}", orgId);
    return quota;
}

/// <summary>
/// Update quota values for an organization (admin operation).
/// </summary>
public async Task<OrganizationQuota> UpdateOrgQuotaAsync(Guid orgId, OrganizationQuota updated)
{
    var quota = await GetOrCreateOrgDefaultAsync(orgId);
    quota.MaxNotebooks = updated.MaxNotebooks;
    quota.MaxEntriesPerNotebook = updated.MaxEntriesPerNotebook;
    quota.MaxEntrySizeBytes = updated.MaxEntrySizeBytes;
    quota.MaxTotalStorageBytes = updated.MaxTotalStorageBytes;
    quota.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    _logger.LogInformation("Updated quota for organization {OrgId}", orgId);
    return quota;
}

/// <summary>
/// Resolve effective quota for a user with inheritance:
/// 1. User-specific quota (highest priority)
/// 2. Organization quota (if user has orgId)
/// 3. System defaults (fallback)
///
/// This method does NOT write to database for inherited values.
/// </summary>
public async Task<UserQuota> GetEffectiveQuotaAsync(string userId, Guid? orgId = null)
{
    // Try user-specific quota first
    var userQuota = await _db.UserQuotas.FindAsync(userId);
    if (userQuota != null)
        return userQuota;

    // Try organization quota
    if (orgId.HasValue)
    {
        var orgQuota = await _db.OrganizationQuotas.FindAsync(orgId.Value);
        if (orgQuota != null)
        {
            // Convert org quota to user quota structure (without saving)
            return new UserQuota
            {
                UserId = userId,
                MaxNotebooks = orgQuota.MaxNotebooks,
                MaxEntriesPerNotebook = orgQuota.MaxEntriesPerNotebook,
                MaxEntrySizeBytes = orgQuota.MaxEntrySizeBytes,
                MaxTotalStorageBytes = orgQuota.MaxTotalStorageBytes,
                CreatedAt = orgQuota.CreatedAt,
                UpdatedAt = orgQuota.UpdatedAt
            };
        }
    }

    // Fall back to system defaults
    return new UserQuota { UserId = userId };
}
```

**Inheritance Logic:**
1. Check if user has custom quota in `UserQuotas` table
2. If not, check organization quota in `OrganizationQuotas` table
3. If not, return system defaults (UserQuota with default field values)
4. Inherited values are read-only (not persisted to UserQuotas)

## 2.4 — Usage Progress Bars on QuotaManagement Page

### Enhanced Page Component

**File:** `frontend/admin/Components/Pages/Admin/QuotaManagement.razor`

Inject `UsageAggregationService`:

```csharp
@inject UsageAggregationService UsageService
```

Load usage stats in `OnInitializedAsync`:

```csharp
private UserUsageStats? usageStats;

protected override async Task OnInitializedAsync()
{
    user = await UserManager.FindByIdAsync(UserId);
    if (user == null)
    {
        errorMessage = "User not found.";
        return;
    }

    quota = await QuotaService.GetOrCreateDefaultAsync(UserId);

    // Load usage stats
    if (user?.AuthorIdHex != null)
    {
        try
        {
            usageStats = await UsageService.GetUserUsageAsync(user.AuthorIdHex);
        }
        catch
        {
            usageStats = new UserUsageStats();
        }
    }
}
```

### Current Usage Card

Add above the quota edit form:

```html
<!-- Current Usage Card -->
<div class="card mb-3">
    <div class="card-header">Current Usage</div>
    <div class="card-body">
        @if (usageStats == null)
        {
            <p class="text-muted">Loading usage statistics...</p>
        }
        else
        {
            <!-- Notebooks Usage -->
            <div class="mb-3">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <small class="text-muted">Notebooks</small>
                    <small class="text-muted">@usageStats.NotebookCount / @quota.MaxNotebooks</small>
                </div>
                <div class="progress" style="height: 20px;">
                    <div class="progress-bar @GetProgressBarClass(usageStats.NotebookCount, quota.MaxNotebooks)"
                         role="progressbar"
                         style="width: @GetProgressPercentage(usageStats.NotebookCount, quota.MaxNotebooks)%">
                        @GetProgressPercentage(usageStats.NotebookCount, quota.MaxNotebooks)%
                    </div>
                </div>
            </div>

            <!-- Total Entries Usage -->
            <div class="mb-3">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <small class="text-muted">Total Entries</small>
                    <small class="text-muted">@usageStats.TotalEntries.ToString("N0")</small>
                </div>
                <p class="text-muted small mb-0">Limit: @quota.MaxEntriesPerNotebook per notebook</p>
            </div>

            <!-- Storage Usage (Estimated) -->
            <div class="mb-3">
                <div class="d-flex justify-content-between align-items-center mb-1">
                    <small class="text-muted">Storage (Estimated)</small>
                    <small class="text-muted">@FormatBytes(usageStats.EstimatedStorageBytes) / @FormatBytes(quota.MaxTotalStorageBytes)</small>
                </div>
                <div class="progress" style="height: 20px;">
                    <div class="progress-bar @GetProgressBarClass(usageStats.EstimatedStorageBytes, quota.MaxTotalStorageBytes)"
                         role="progressbar"
                         style="width: @GetProgressPercentage(usageStats.EstimatedStorageBytes, quota.MaxTotalStorageBytes)%">
                        @GetProgressPercentage(usageStats.EstimatedStorageBytes, quota.MaxTotalStorageBytes)%
                    </div>
                </div>
                <small class="text-muted d-block mt-1">Note: Storage is estimated (1KB per entry). Exact usage requires backend enhancement.</small>
            </div>
        }
    </div>
</div>
```

### Helper Methods

```csharp
private static string FormatBytes(long bytes)
{
    if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
    if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
    if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
    return $"{bytes} B";
}

private static string GetProgressBarClass(long current, long max)
{
    if (max == 0) return "bg-success";
    double percentage = (double)current / max * 100;
    return percentage >= 90 ? "bg-danger" :
           percentage >= 75 ? "bg-warning" :
           "bg-success";
}

private static int GetProgressPercentage(long current, long max)
{
    if (max == 0) return 0;
    return Math.Min((int)((double)current / max * 100), 100);
}
```

**Color Coding:**
- Green: < 75% usage
- Yellow: 75-90% usage
- Red: ≥ 90% usage

## 2.5 — Organization Quota UI

### Organization Detail Enhancement

**File:** `frontend/admin/Components/Pages/Admin/OrganizationDetail.razor`

Inject `QuotaService`:

```csharp
@inject QuotaService QuotaService
```

Load org quota in `OnInitializedAsync`:

```csharp
private OrganizationQuota? orgQuota;

protected override async Task OnInitializedAsync()
{
    // ... existing code ...
    await Task.WhenAll(LoadGroups(), LoadClearances(), LoadOrgQuota());
}

private async Task LoadOrgQuota()
{
    try
    {
        orgQuota = await QuotaService.GetOrCreateOrgDefaultAsync(OrgId);
    }
    catch { /* quota card will show loading state */ }
}
```

### Default Quota Limits Card

Add after Security Clearances card:

```html
<div class="card mt-4">
    <div class="card-header">Default Quota Limits</div>
    <div class="card-body">
        <p class="text-muted">These defaults apply to all users in this organization who don't have custom quotas.</p>

        @if (orgQuota == null)
        {
            <p class="text-muted">Loading quota settings...</p>
        }
        else
        {
            <EditForm Model="orgQuota" OnValidSubmit="SaveOrgQuota" FormName="orgQuotaEdit">
                <div class="mb-3">
                    <label class="form-label">Max Notebooks</label>
                    <InputNumber class="form-control" @bind-Value="orgQuota.MaxNotebooks" />
                </div>
                <div class="mb-3">
                    <label class="form-label">Max Entries per Notebook</label>
                    <InputNumber class="form-control" @bind-Value="orgQuota.MaxEntriesPerNotebook" />
                </div>
                <div class="mb-3">
                    <label class="form-label">Max Entry Size (bytes)</label>
                    <InputNumber class="form-control" @bind-Value="orgQuota.MaxEntrySizeBytes" />
                    <small class="text-muted">Default: 10,485,760 (10 MB). Display: @FormatBytes(orgQuota.MaxEntrySizeBytes)</small>
                </div>
                <div class="mb-3">
                    <label class="form-label">Max Total Storage (bytes)</label>
                    <InputNumber class="form-control" @bind-Value="orgQuota.MaxTotalStorageBytes" />
                    <small class="text-muted">Default: 1,073,741,824 (1 GB). Display: @FormatBytes(orgQuota.MaxTotalStorageBytes)</small>
                </div>
                <button type="submit" class="btn btn-primary">Save Quota Defaults</button>
            </EditForm>

            <div class="mt-3 text-muted">
                <small>Created: @orgQuota.CreatedAt.ToString("u") | Updated: @orgQuota.UpdatedAt.ToString("u")</small>
            </div>
        }
    </div>
</div>
```

### Save Handler

```csharp
private async Task SaveOrgQuota()
{
    if (orgQuota == null) return;
    isSubmitting = true;
    errorMessage = null;

    try
    {
        await QuotaService.UpdateOrgQuotaAsync(OrgId, orgQuota);
        errorMessage = null;
        // Re-load to get updated timestamps
        await LoadOrgQuota();
    }
    catch (Exception ex)
    {
        errorMessage = $"Failed to save organization quota: {ex.Message}";
    }
    finally { isSubmitting = false; }
}
```

## 2.6 — Testing

### Unit Tests: Quota Service

```csharp
[Fact]
public async Task GetOrCreateOrgDefaultAsync_CreatesQuotaWithDefaults()
{
    var orgId = Guid.NewGuid();
    var quota = await _quotaService.GetOrCreateOrgDefaultAsync(orgId);

    Assert.NotNull(quota);
    Assert.Equal(orgId, quota.OrganizationId);
    Assert.Equal(50, quota.MaxNotebooks);
    Assert.Equal(5000, quota.MaxEntriesPerNotebook);
    Assert.Equal(10_485_760, quota.MaxEntrySizeBytes);
    Assert.Equal(1_073_741_824, quota.MaxTotalStorageBytes);
}

[Fact]
public async Task GetEffectiveQuotaAsync_ReturnUserQuotaWhenExists()
{
    // Create user quota
    var userId = "user123";
    var userQuota = new UserQuota { UserId = userId, MaxNotebooks = 10 };
    _db.UserQuotas.Add(userQuota);
    await _db.SaveChangesAsync();

    // Even with orgId, user quota takes precedence
    var effective = await _quotaService.GetEffectiveQuotaAsync(userId, Guid.NewGuid());
    Assert.Equal(10, effective.MaxNotebooks);
}

[Fact]
public async Task GetEffectiveQuotaAsync_ReturnOrgQuotaWhenUserQuotaNotExists()
{
    var userId = "user123";
    var orgId = Guid.NewGuid();
    var orgQuota = new OrganizationQuota { OrganizationId = orgId, MaxNotebooks = 25 };
    _db.OrganizationQuotas.Add(orgQuota);
    await _db.SaveChangesAsync();

    var effective = await _quotaService.GetEffectiveQuotaAsync(userId, orgId);
    Assert.Equal(25, effective.MaxNotebooks);
}

[Fact]
public async Task GetEffectiveQuotaAsync_ReturnDefaultsWhenNoOrgQuota()
{
    var userId = "user123";
    var effective = await _quotaService.GetEffectiveQuotaAsync(userId, null);

    // Should return system defaults
    Assert.Equal(5, effective.MaxNotebooks);
    Assert.Equal(1000, effective.MaxEntriesPerNotebook);
}
```

### Integration Tests: UI

**QuotaManagement.razor:**
1. Load page for user with usage stats
2. Verify progress bars display correctly
3. Verify color coding (green/yellow/red)
4. Verify quota edit form works

**OrganizationDetail.razor:**
1. Load organization page
2. Verify quota defaults card appears
3. Edit quota limits
4. Verify save button updates database
5. Refresh and confirm persistence

## 2.7 — Verification

### Database Migration

```bash
cd infrastructure/postgres
psql -U postgres -d notebook_admin -f migrations/admin/022_admin_organization_quotas.sql

# Verify schema
psql -U postgres -d notebook_admin -c "\d \"OrganizationQuotas\""
```

Expected output:
```
                  Table "public.OrganizationQuotas"
        Column        |           Type           | Collation | Nullable | Default
---------------------+--------------------------+-----------+----------+---------
 OrganizationId      | uuid                     |           | not null |
 MaxNotebooks        | integer                  |           | not null |
 MaxEntriesPerNotebook | integer                |           | not null |
 MaxEntrySizeBytes   | bigint                   |           | not null |
 MaxTotalStorageBytes| bigint                   |           | not null |
 CreatedAt           | timestamp with time zone |           | not null |
 UpdatedAt           | timestamp with time zone |           | not null |
Indexes:
    "PK_OrganizationQuotas" PRIMARY KEY, btree ("OrganizationId")
```

### Manual Testing Checklist

- [ ] Build project: `dotnet build` succeeds
- [ ] Create organization "TestOrg"
- [ ] Navigate to `/admin/organizations/{id}`
- [ ] Scroll to "Default Quota Limits" card
- [ ] Edit values and save (e.g., MaxNotebooks = 20)
- [ ] Refresh page, verify values persist
- [ ] Create user assigned to TestOrg
- [ ] Navigate to user quota page
- [ ] Verify inherited values from org (20 notebooks)
- [ ] Create custom user quota (10 notebooks)
- [ ] Verify custom value overrides org default
- [ ] Create notebooks to increase usage
- [ ] Verify progress bars on QuotaManagement page
- [ ] Check color coding: Green < 75%, Yellow 75-90%, Red ≥ 90%

### End-to-End Test Flow

1. Create organization with specific quotas
2. Create user in organization without custom quota
3. Verify user sees inherited organization quotas
4. Add custom quota to user
5. Verify custom quota overrides organization default
6. Create notebooks and entries
7. Verify usage progress bars update correctly
8. Modify organization quota
9. Verify change does NOT affect user with custom quota

## Deliverables

- ✅ OrganizationQuota model and SQL migration
- ✅ QuotaService inheritance methods (GetOrCreateOrgDefaultAsync, UpdateOrgQuotaAsync, GetEffectiveQuotaAsync)
- ✅ Usage progress bars on QuotaManagement.razor
- ✅ Organization quota editing UI on OrganizationDetail.razor
- ✅ Database migration in `infrastructure/postgres/migrations/admin/`
- ✅ Documentation in README.md and CLAUDE.md
- ✅ Build succeeds with no new errors

## Key Design Decisions

**Inheritance Model:**
- User quota (highest priority) → Organization quota → System defaults
- Inherited values are read-only (not persisted to UserQuotas table)
- Allows per-user overrides while providing sensible org-level defaults

**Database Separation:**
- Admin database (`notebook_admin`) separate from backend (`thinktank`)
- OrganizationId is external reference (no foreign key)
- Enables independent deployment and scaling

**Migration Organization:**
- Separate `admin/` and `server/` directories for clarity
- SQL migrations for production deployment
- EF Core configuration retained for local development

**Usage Visualization:**
- Estimated storage (1KB per entry) for quick feedback
- Color-coded progress bars for at-a-glance status
- Real-time data from UsageAggregationService

---

**Status:** ✅ COMPLETE
**Commits:** 9fbb825 (implementation), 8707158 (SQL migration), 140b0c6 (organization), ae8ecaa (README), 91309ee (CLAUDE.md)
