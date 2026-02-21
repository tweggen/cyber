using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Entities;
using Notebook.Server.Services.Crawlers;
using System.Text.Json;

namespace Notebook.Server.Services;

/// <summary>
/// Service for managing crawlers and executing crawls.
/// Handles configuration, execution, and state management.
/// </summary>
public sealed class CrawlerService
{
    private readonly NotebookDbContext _context;
    private readonly ConfluenceCrawler _confluenceCrawler;
    private readonly CrawlerConfigValidator _configValidator;
    private readonly ILogger<CrawlerService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public CrawlerService(
        NotebookDbContext context,
        ConfluenceCrawler confluenceCrawler,
        CrawlerConfigValidator configValidator,
        ILogger<CrawlerService> logger,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _confluenceCrawler = confluenceCrawler;
        _configValidator = configValidator;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Configure a Confluence crawler for a notebook.
    /// </summary>
    public async Task<CrawlerConfigResponse> ConfigureConfluenceCrawlerAsync(
        Guid notebookId,
        string configJson,
        Guid userId,
        Guid organizationId)
    {
        try
        {
            // Validate configuration
            _configValidator.ValidateConfluenceConfig(configJson);

            // Check if notebook exists
            var notebook = await _context.Notebooks.FindAsync(notebookId)
                ?? throw new ArgumentException($"Notebook {notebookId} not found");

            // Create or update Confluence state
            var confluenceState = new ConfluenceCrawlerStateEntity
            {
                Config = configJson,
                SyncState = "{}"
            };

            await _context.ConfluenceCrawlerStates.AddAsync(confluenceState);
            await _context.SaveChangesAsync();

            // Create or update crawler record
            var existingCrawler = await _context.Crawlers
                .FirstOrDefaultAsync(c =>
                    c.NotebookId == notebookId &&
                    c.SourceType == "confluence");

            if (existingCrawler != null)
            {
                existingCrawler.StateRefId = confluenceState.Id;
                existingCrawler.UpdatedAt = DateTime.UtcNow;
                _context.Crawlers.Update(existingCrawler);
            }
            else
            {
                var config = ConfluenceConfig.FromJson(configJson, _configValidator);

                var crawler = new CrawlerEntity
                {
                    NotebookId = notebookId,
                    Name = $"Confluence:{config.SpaceKey}",
                    SourceType = "confluence",
                    StateProvider = "confluence_state",
                    StateRefId = confluenceState.Id,
                    IsEnabled = true,
                    CreatedBy = userId,
                    OrganizationId = organizationId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.Crawlers.AddAsync(crawler);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                $"Configured Confluence crawler for notebook {notebookId}");

            return new CrawlerConfigResponse
            {
                Success = true,
                Message = "Confluence crawler configured successfully",
                CrawlerStateId = confluenceState.Id
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Configuration error: {ex.Message}");
            return new CrawlerConfigResponse
            {
                Success = false,
                Message = $"Configuration error: {ex.Message}",
                Error = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Confluence crawler");
            return new CrawlerConfigResponse
            {
                Success = false,
                Message = "Failed to configure crawler",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Test Confluence crawler connection and configuration.
    /// </summary>
    public async Task<CrawlerTestResponse> TestConfluenceCrawlerAsync(string configJson)
    {
        try
        {
            // Validate configuration
            _configValidator.ValidateConfluenceConfig(configJson);
            var config = ConfluenceConfig.FromJson(configJson, _configValidator);

            // Try to connect and fetch space info
            var apiClientLogger = _loggerFactory.CreateLogger<ConfluenceApiClient>();
            await using var apiClient = new ConfluenceApiClient(
                config.BaseUrl, config.Username, config.ApiToken, apiClientLogger);

            var space = await apiClient.GetSpaceAsync(config.SpaceKey);

            _logger.LogInformation(
                $"Successfully tested Confluence connection to space {space.Key}");

            return new CrawlerTestResponse
            {
                Success = true,
                Message = $"Connected to Confluence space '{space.Name}' ({space.Key})",
                SpaceInfo = new { space.Id, space.Key, space.Name }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning($"Confluence API error: {ex.Message}");
            return new CrawlerTestResponse
            {
                Success = false,
                Message = $"Confluence API error: {ex.Message}",
                Error = ex.Message
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Configuration error: {ex.Message}");
            return new CrawlerTestResponse
            {
                Success = false,
                Message = $"Configuration error: {ex.Message}",
                Error = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Confluence crawler");
            return new CrawlerTestResponse
            {
                Success = false,
                Message = "Failed to test connection",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Execute a Confluence crawler synchronously.
    /// </summary>
    public async Task<CrawlerRunResponse> RunConfluenceCrawlerAsync(Guid notebookId)
    {
        try
        {
            // Get crawler configuration
            var crawler = await _context.Crawlers
                .FirstOrDefaultAsync(c =>
                    c.NotebookId == notebookId &&
                    c.SourceType == "confluence")
                ?? throw new ArgumentException($"No Confluence crawler found for notebook {notebookId}");

            var confluenceState = await _context.ConfluenceCrawlerStates
                .FindAsync(crawler.StateRefId)
                ?? throw new InvalidOperationException("Crawler state not found");

            // Parse configuration and state
            var config = ConfluenceConfig.FromJson(confluenceState.Config, _configValidator);
            var previousState = ConfluenceSyncState.FromJson(confluenceState.SyncState, _configValidator);

            // Execute crawl
            _logger.LogInformation($"Starting Confluence crawler for notebook {notebookId}");
            var crawlResult = await _confluenceCrawler.CrawlAsync(config, previousState);

            // Record run in database
            var crawlerRun = new CrawlerRunEntity
            {
                CrawlerId = crawler.Id,
                StartedAt = crawlResult.StartedAt,
                CompletedAt = crawlResult.CompletedAt,
                Status = crawlResult.Status,
                EntriesCreated = crawlResult.EntriesCreated,
                ErrorMessage = crawlResult.ErrorMessage,
                Stats = crawlResult.Stats != null
                    ? JsonSerializer.Serialize(crawlResult.Stats)
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            await _context.CrawlerRuns.AddAsync(crawlerRun);

            // Update crawler sync tracking
            crawler.LastSyncAt = crawlResult.CompletedAt ?? DateTime.UtcNow;
            crawler.LastSyncStatus = crawlResult.Status;
            crawler.LastError = crawlResult.ErrorMessage;
            crawler.UpdatedAt = DateTime.UtcNow;

            _context.Crawlers.Update(crawler);

            // Update crawler state if successful
            if (crawlResult.Status == "success" && crawlResult.NewState != null)
            {
                confluenceState.SyncState = crawlResult.NewState.ToJson();
                confluenceState.UpdatedAt = DateTime.UtcNow;
                _context.ConfluenceCrawlerStates.Update(confluenceState);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                $"Confluence crawler completed: {crawlResult.EntriesCreated} entries created");

            return new CrawlerRunResponse
            {
                Success = crawlResult.Status == "success",
                Status = crawlResult.Status,
                EntriesCreated = crawlResult.EntriesCreated,
                Duration = crawlResult.Duration.TotalSeconds,
                Message = crawlResult.Status == "success"
                    ? $"Successfully created {crawlResult.EntriesCreated} entries"
                    : crawlResult.ErrorMessage ?? "Crawler failed",
                RunId = crawlerRun.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Confluence crawler");
            return new CrawlerRunResponse
            {
                Success = false,
                Status = "failed",
                Message = $"Failed to run crawler: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Get crawler run history.
    /// </summary>
    public async Task<List<CrawlerRunHistory>> GetCrawlerRunsAsync(
        Guid notebookId,
        int limit = 50)
    {
        var crawler = await _context.Crawlers
            .FirstOrDefaultAsync(c =>
                c.NotebookId == notebookId &&
                c.SourceType == "confluence");

        if (crawler == null)
            return new();

        var runs = await _context.CrawlerRuns
            .Where(r => r.CrawlerId == crawler.Id)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .Select(r => new CrawlerRunHistory
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Status = r.Status,
                EntriesCreated = r.EntriesCreated,
                Duration = r.CompletedAt.HasValue
                    ? (r.CompletedAt.Value - r.StartedAt).TotalSeconds
                    : null,
                ErrorMessage = r.ErrorMessage,
                Stats = r.Stats != null ? JsonSerializer.Deserialize<object>(r.Stats) : null
            })
            .ToListAsync();

        return runs;
    }

    /// <summary>
    /// Get crawler configuration.
    /// </summary>
    public async Task<CrawlerConfigResponse> GetCrawlerConfigAsync(Guid notebookId)
    {
        try
        {
            var crawler = await _context.Crawlers
                .FirstOrDefaultAsync(c =>
                    c.NotebookId == notebookId &&
                    c.SourceType == "confluence");

            if (crawler == null)
                return new CrawlerConfigResponse
                {
                    Success = false,
                    Message = "No Confluence crawler found for this notebook"
                };

            var state = await _context.ConfluenceCrawlerStates
                .FindAsync(crawler.StateRefId);

            if (state == null)
                return new CrawlerConfigResponse
                {
                    Success = false,
                    Message = "Crawler state not found"
                };

            return new CrawlerConfigResponse
            {
                Success = true,
                Message = "Configuration retrieved",
                ConfigJson = state.Config,
                LastSyncAt = crawler.LastSyncAt,
                LastSyncStatus = crawler.LastSyncStatus,
                IsEnabled = crawler.IsEnabled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get crawler configuration");
            return new CrawlerConfigResponse
            {
                Success = false,
                Message = $"Failed to retrieve configuration: {ex.Message}",
                Error = ex.Message
            };
        }
    }
}

// ============= DTOs =============

public class CrawlerConfigResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? ConfigJson { get; set; }
    public Guid? CrawlerStateId { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Error { get; set; }
}

public class CrawlerTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? SpaceInfo { get; set; }
    public string? Error { get; set; }
}

public class CrawlerRunResponse
{
    public bool Success { get; set; }
    public string Status { get; set; } = "";
    public int EntriesCreated { get; set; }
    public double Duration { get; set; }
    public string Message { get; set; } = "";
    public Guid? RunId { get; set; }
    public string? Error { get; set; }
}

public class CrawlerRunHistory
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "";
    public int EntriesCreated { get; set; }
    public double? Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Stats { get; set; }
}
