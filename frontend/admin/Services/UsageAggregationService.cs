using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// Service to aggregate user quota usage from the notebook API.
/// </summary>
public class UsageAggregationService
{
    private readonly NotebookApiClient _apiClient;

    public UsageAggregationService(NotebookApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Get aggregated usage statistics for a user based on their notebooks.
    /// </summary>
    public async Task<UserUsageStats> GetUserUsageAsync(string authorIdHex)
    {
        try
        {
            var response = await _apiClient.ListNotebooksAsync(authorIdHex);
            var ownedNotebooks = response?.Notebooks?
                .Where(n => n.IsOwner)
                .ToList() ?? [];

            var totalEntries = ownedNotebooks.Sum(n => (long)n.TotalEntries);
            var totalEntropy = ownedNotebooks.Sum(n => n.TotalEntropy);

            // Estimate storage: average ~1KB per entry
            var estimatedStorageBytes = totalEntries * 1024;

            return new UserUsageStats
            {
                NotebookCount = ownedNotebooks.Count,
                TotalEntries = totalEntries,
                TotalEntropy = totalEntropy,
                EstimatedStorageBytes = estimatedStorageBytes
            };
        }
        catch
        {
            // Return empty stats if API call fails
            return new UserUsageStats
            {
                NotebookCount = 0,
                TotalEntries = 0,
                TotalEntropy = 0,
                EstimatedStorageBytes = 0
            };
        }
    }
}

/// <summary>
/// Aggregated usage statistics for a user.
/// </summary>
public record UserUsageStats
{
    public int NotebookCount { get; init; }
    public long TotalEntries { get; init; }
    public double TotalEntropy { get; init; }
    public long EstimatedStorageBytes { get; init; }
}
