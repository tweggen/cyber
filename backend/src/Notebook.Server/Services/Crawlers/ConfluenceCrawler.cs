using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Confluence crawler: fetches pages from a Confluence space and converts them to notebook entries.
/// Supports incremental syncs via content hashing and timestamp tracking.
/// </summary>
public sealed class ConfluenceCrawler
{
    private readonly ILogger<ConfluenceCrawler> _logger;
    private readonly IContentFilterPipeline _contentFilterPipeline;

    public ConfluenceCrawler(
        ILogger<ConfluenceCrawler> logger,
        IContentFilterPipeline contentFilterPipeline)
    {
        _logger = logger;
        _contentFilterPipeline = contentFilterPipeline;
    }

    /// <summary>
    /// Crawl a Confluence space and return entries for the notebook.
    /// </summary>
    public async Task<CrawlerResult> CrawlAsync(
        ConfluenceConfig config,
        ConfluenceSyncState previousState)
    {
        var result = new CrawlerResult { StartedAt = DateTime.UtcNow };
        var entries = new List<NotebookBatchEntry>();
        var newState = new ConfluenceSyncState { SpaceKey = config.SpaceKey };

        try
        {
            // Create a logger for the API client (cast the generic logger)
            var apiClientLogger = (ILogger<ConfluenceApiClient>)(object)_logger;

            await using var apiClient = new ConfluenceApiClient(
                config.BaseUrl, config.Username, config.ApiToken, apiClientLogger);

            // Get space info
            var space = await apiClient.GetSpaceAsync(config.SpaceKey);
            newState.SpaceId = space.Id;
            newState.SpaceKey = config.SpaceKey;
            _logger.LogInformation($"Crawling Confluence space {config.SpaceKey} (ID: {space.Id})");

            // Fetch pages with pagination
            var pageCount = 0;
            var pageLimit = config.MaxPages > 0 ? config.MaxPages : int.MaxValue;
            string? cursor = null;

            do
            {
                var (pages, nextCursor) = await apiClient.GetPagesAsync(
                    config.SpaceKey,
                    limit: 25,
                    cursor: cursor,
                    includeLabels: config.IncludeLabels.Any() ? config.IncludeLabels : null,
                    excludeLabels: config.ExcludeLabels.Any() ? config.ExcludeLabels : null);

                if (!pages.Any())
                    break;

                foreach (var page in pages)
                {
                    if (pageCount >= pageLimit)
                        break;

                    var entry = await ConvertPageToEntryAsync(page, config);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }

                    // Track in sync state
                    var contentHash = ComputeHash(page.HtmlBody);
                    newState.PageMetadata[page.Id] = new ConfluencePageMetadata
                    {
                        Title = page.Title,
                        Version = page.Version.Number,
                        LastModified = page.Version.CreatedAt,
                        Status = page.Status,
                        ContentHash = contentHash
                    };

                    pageCount++;
                }

                cursor = nextCursor;
            } while (!string.IsNullOrEmpty(cursor) && pageCount < pageLimit);

            newState.LastSyncTimestamp = DateTime.UtcNow;
            newState.PagesSynced = pageCount;

            result.Status = "success";
            result.EntriesCreated = entries.Count;
            result.Entries = entries;
            result.NewState = newState;
            result.Stats = new
            {
                PagesFetched = pageCount,
                EntriesCreated = entries.Count,
                BytesProcessed = entries.Sum(e => e.Content.Length)
            };

            _logger.LogInformation(
                $"Confluence crawler completed: {pageCount} pages → {entries.Count} entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confluence crawler failed");
            result.Status = "failed";
            result.ErrorMessage = ex.Message;
            result.NewState = newState;
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Convert a Confluence page to a notebook entry.
    /// Applies content filtering and adds source attribution.
    /// </summary>
    private async Task<NotebookBatchEntry?> ConvertPageToEntryAsync(
        ConfluencePage page,
        ConfluenceConfig config)
    {
        try
        {
            // Apply content filter to clean boilerplate
            var filterResult = _contentFilterPipeline.Apply(page.HtmlBody, null);
            var content = filterResult.Content;

            // If the filter removed too much, try a different approach
            if (string.IsNullOrWhiteSpace(content))
            {
                // Fall back to using raw HTML wrapped in code block
                content = $"```html\n{page.HtmlBody}\n```";
            }

            // Add title as markdown heading if not already there
            if (!content.StartsWith("#"))
            {
                content = $"# {page.Title}\n\n{content}";
            }

            return new NotebookBatchEntry
            {
                Content = content,
                ContentType = "text/markdown",
                SourceHint = "confluence",
                Metadata = new Dictionary<string, object>
                {
                    ["source_url"] = page.WebUrl,
                    ["source_type"] = "confluence",
                    ["confluence_page_id"] = page.Id,
                    ["confluence_space_key"] = config.SpaceKey,
                    ["confluence_version"] = page.Version.Number,
                    ["confluence_status"] = page.Status,
                    ["last_modified"] = page.Version.CreatedAt.ToString("O"),
                    ["labels"] = page.Labels,
                    ["source_attribution"] = $"Confluence:Space:{config.SpaceKey}:Page:{page.Id}",
                    ["source_crawled_at"] = DateTime.UtcNow.ToString("O")
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to convert page {page.Id} ({page.Title}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Compute SHA256 hash of content (for duplicate detection).
    /// </summary>
    private static string ComputeHash(string content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Result of a crawler run.
/// </summary>
public sealed class CrawlerResult
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running"; // running | success | failed
    public List<NotebookBatchEntry> Entries { get; set; } = new();
    public ConfluenceSyncState? NewState { get; set; }
    public int EntriesCreated { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Stats { get; set; }

    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// Batch entry for the notebook API.
/// </summary>
public sealed class NotebookBatchEntry
{
    public required string Content { get; set; }
    public required string ContentType { get; set; }
    public string? SourceHint { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
