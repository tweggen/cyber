using Microsoft.EntityFrameworkCore;
using NotebookAdmin.Data;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// Manages per-user resource quotas.
/// </summary>
public class QuotaService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<QuotaService> _logger;

    public QuotaService(ApplicationDbContext db, ILogger<QuotaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get the quota for a user, creating a default one if it doesn't exist.
    /// </summary>
    public async Task<UserQuota> GetOrCreateDefaultAsync(string userId)
    {
        var quota = await _db.UserQuotas.FindAsync(userId);
        if (quota != null)
            return quota;

        quota = new UserQuota { UserId = userId };
        _db.UserQuotas.Add(quota);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created default quota for user {UserId}", userId);
        return quota;
    }

    /// <summary>
    /// Update quota values for a user (admin operation).
    /// </summary>
    public async Task<UserQuota> UpdateQuotaAsync(string userId, UserQuota updated)
    {
        var quota = await GetOrCreateDefaultAsync(userId);
        quota.MaxNotebooks = updated.MaxNotebooks;
        quota.MaxEntriesPerNotebook = updated.MaxEntriesPerNotebook;
        quota.MaxEntrySizeBytes = updated.MaxEntrySizeBytes;
        quota.MaxTotalStorageBytes = updated.MaxTotalStorageBytes;
        quota.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated quota for user {UserId}", userId);
        return quota;
    }

    /// <summary>
    /// Check if a user can create a new notebook, based on current count from the API.
    /// </summary>
    public async Task<bool> CanCreateNotebookAsync(
        string userId, string authorIdHex, NotebookApiClient apiClient)
    {
        var quota = await GetOrCreateDefaultAsync(userId);
        try
        {
            var response = await apiClient.ListNotebooksAsync(authorIdHex);
            var ownedCount = response?.Notebooks.Count(n => n.IsOwner) ?? 0;
            return ownedCount < quota.MaxNotebooks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check notebook count for quota, allowing by default");
            return true;
        }
    }

    /// <summary>
    /// Check if a user can create a new entry, based on entry count and size.
    /// </summary>
    public async Task<bool> CanCreateEntryAsync(
        string userId, string authorIdHex, Guid notebookId,
        long entrySizeBytes, NotebookApiClient apiClient)
    {
        var quota = await GetOrCreateDefaultAsync(userId);

        if (entrySizeBytes > quota.MaxEntrySizeBytes)
            return false;

        try
        {
            var response = await apiClient.BrowseAsync(authorIdHex, notebookId);
            var entryCount = response?.TotalEntries ?? 0;
            return entryCount < (uint)quota.MaxEntriesPerNotebook;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check entry count for quota, allowing by default");
            return true;
        }
    }
}
