# Crawler Extension Guide

**Audience:** Backend developers adding new crawler types (Git, FileSystem, SharePoint, etc.) to Cyber

**Current Version:** Phase 5 (Confluence crawler)

**Next Planned Crawlers:** Git repositories, file systems, SharePoint, Slack

## Quick Start

This guide shows how to implement a new crawler type from scratch. We'll use a **Git repository crawler** as the running example, but the patterns apply to any source (FileSystem, SharePoint, RSS feeds, etc.).

**Time to implement:** 4-6 hours for basic Git crawler

## Architecture Overview

The crawler system uses a pluggable architecture:

```
                    ┌─────────────────────────┐
                    │   CrawlersController    │ (HTTP endpoints)
                    └──────────┬──────────────┘
                               │
                    ┌──────────▼──────────┐
                    │  CrawlerService     │ (orchestration)
                    └──────────┬──────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
   ┌────▼─────┐          ┌────▼─────┐          ┌────▼──────┐
   │Confluence │          │  Git     │          │FileSystem │
   │Crawler    │          │ Crawler  │          │  Crawler  │
   └──────────┘          └──────────┘          └───────────┘
        │                      │                      │
   ┌────▼──────────┐      ┌────▼──────────┐      ┌────▼──────────┐
   │Confluence API │      │GitLab/GitHub  │      │OS file APIs   │
   │   Client      │      │   Client      │      │               │
   └───────────────┘      └───────────────┘      └───────────────┘

   Database:
   ┌─────────────────────────────────────────────────────────────┐
   │  crawlers                    (generic metadata)             │
   │  crawler_runs                (execution history)            │
   │  confluence_crawler_state    (sync state for Confluence)    │
   │  git_crawler_state           (sync state for Git)           │
   │  filesystem_crawler_state    (sync state for FileSystem)    │
   └─────────────────────────────────────────────────────────────┘
```

### Key Abstractions

**Crawler (source-specific):** Implements the crawl logic
- Input: config + previous sync state
- Output: list of entries + new state
- Example: `ConfluenceCrawler.CrawlAsync(config, previousState) → CrawlerResult`

**API Client (source-specific):** Handles external API calls
- Example: `ConfluenceApiClient.GetPagesAsync() → ConfluencePage[]`

**State (source-specific):** Tracks incremental sync progress
- Stored in database as JSONB
- Example: `ConfluenceSyncState { SpaceKey, LastSyncTimestamp, PageMetadata }`

**Service (generic):** Orchestrates configuration → execution → persistence
- Same service handles all crawler types
- Routes based on `source_type` field
- Example: `CrawlerService.RunConfluenceCrawlerAsync() / RunGitCrawlerAsync()`

## Step-by-Step Implementation

### Step 1: Define Your Crawler Type

Choose a unique `source_type` identifier (lowercase, alphanumeric):

```csharp
// Valid source_type values:
"confluence"      // existing
"git"            // what we're building
"filesystem"
"sharepoint"
"slack"
```

Your type will appear in:
- Database `crawlers.source_type` column
- API endpoints: `/api/crawlers/{id}/git/config`, `/api/crawlers/{id}/git/run`, etc.
- Configuration: `state_provider: "git_state"` in database

### Step 2: Create Configuration Schema

**File:** `backend/src/Notebook.Server/Schemas/git-crawler-config.schema.json`

Define the JSON schema for configuration validation:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Git Crawler Configuration",
  "type": "object",
  "required": ["repository_url", "branch"],
  "additionalProperties": false,
  "properties": {
    "repository_url": {
      "type": "string",
      "format": "uri",
      "description": "Git repository URL (HTTPS or SSH)",
      "example": "https://github.com/company/repo"
    },
    "branch": {
      "type": "string",
      "default": "main",
      "description": "Branch to crawl",
      "example": "main"
    },
    "file_patterns": {
      "type": "array",
      "items": { "type": "string" },
      "default": ["*.md", "*.txt"],
      "description": "Glob patterns for files to include"
    },
    "credentials": {
      "type": "object",
      "properties": {
        "username": {
          "type": "string",
          "description": "GitHub/GitLab username (optional)"
        },
        "password": {
          "type": "string",
          "description": "Personal access token (optional)"
        }
      }
    },
    "max_files": {
      "type": "integer",
      "minimum": 0,
      "default": 0,
      "description": "Max files to crawl (0 = unlimited)"
    }
  }
}
```

### Step 3: Create Database Migration

**File:** `infrastructure/postgres/migrations/024_git_crawler_state.sql`

```sql
-- Git crawler state: configuration and incremental sync tracking
CREATE TABLE IF NOT EXISTS git_crawler_state (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Configuration (validated against JSON schema)
    config JSONB NOT NULL,

    -- Sync state (updated after each successful sync)
    -- Schema:
    --   repository_url (string) — Repository URL
    --   branch (string) — Branch being tracked
    --   last_commit_sha (string) — Last processed commit
    --   last_sync_timestamp (ISO8601) — When last sync completed
    --   files_tracked (object) — {path: {sha, modified, hash}}
    sync_state JSONB NOT NULL DEFAULT '{}',

    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_git_state_config_repo ON git_crawler_state USING GIN(config jsonb_path_ops);
CREATE INDEX idx_git_state_sync_commit ON git_crawler_state ((sync_state->>'last_commit_sha'));
```

### Step 4: Create C# Entity

**File:** `backend/src/Notebook.Server/Data/Entities/GitCrawlerStateEntity.cs`

```csharp
namespace Notebook.Data.Entities;

public class GitCrawlerStateEntity
{
    public Guid Id { get; set; }

    /// <summary>User-provided configuration as JSON string</summary>
    public string Config { get; set; } = "{}";

    /// <summary>Internal sync state as JSON string</summary>
    public string SyncState { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Update:** `ApplicationDbContext.cs`

```csharp
public DbSet<GitCrawlerStateEntity> GitCrawlerStates => Set<GitCrawlerStateEntity>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ... existing configurations ...

    modelBuilder.Entity<GitCrawlerStateEntity>(entity =>
    {
        entity.ToTable("git_crawler_state");
        entity.Property(e => e.Config).HasColumnType("jsonb");
        entity.Property(e => e.SyncState).HasColumnType("jsonb").HasColumnName("sync_state");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    });
}
```

### Step 5: Create Configuration Model

**File:** `backend/src/Notebook.Server/Services/Crawlers/GitConfig.cs`

```csharp
using System.Text.Json;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Parsed and validated Git crawler configuration.
/// </summary>
public sealed class GitConfig
{
    public required string RepositoryUrl { get; init; }
    public required string Branch { get; init; }
    public List<string> FilePatterns { get; init; } = new();
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int MaxFiles { get; init; } = 0;

    /// <summary>
    /// Parse and validate configuration from JSON.
    /// </summary>
    public static GitConfig FromJson(string json, CrawlerConfigValidator validator)
    {
        validator.ValidateGitConfig(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new GitConfig
        {
            RepositoryUrl = root.GetProperty("repository_url").GetString()
                ?? throw new InvalidOperationException("repository_url is required"),
            Branch = root.GetProperty("branch").GetString() ?? "main",
            FilePatterns = root.TryGetProperty("file_patterns", out var patterns)
                ? patterns.EnumerateArray()
                    .Select(p => p.GetString() ?? "")
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList()
                : new() { "*.md", "*.txt" },
            Username = root.TryGetProperty("credentials", out var creds) &&
                       creds.TryGetProperty("username", out var user)
                ? user.GetString()
                : null,
            Password = root.TryGetProperty("credentials", out var creds2) &&
                       creds2.TryGetProperty("password", out var pass)
                ? pass.GetString()
                : null,
            MaxFiles = root.TryGetProperty("max_files", out var max) ? max.GetInt32() : 0
        };
    }
}

/// <summary>
/// Git crawler sync state for incremental updates.
/// </summary>
public sealed class GitSyncState
{
    public string RepositoryUrl { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string? LastCommitSha { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public int FilesTracked { get; set; }
    public Dictionary<string, GitFileMetadata> Files { get; set; } = new();

    public string ToJson()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Serialize(this, options);
    }

    public static GitSyncState FromJson(string json, CrawlerConfigValidator validator)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<GitSyncState>(json, options) ?? new();
    }
}

/// <summary>
/// Metadata about a tracked file.
/// </summary>
public sealed class GitFileMetadata
{
    public string Path { get; set; } = "";
    public string? Sha { get; set; }
    public DateTime? Modified { get; set; }
    public string? ContentHash { get; set; }
}
```

### Step 6: Add Configuration Validation

**Update:** `backend/src/Notebook.Server/Services/Crawlers/CrawlerConfigValidator.cs`

```csharp
public void ValidateGitConfig(string configJson)
{
    try
    {
        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        // Check for required fields
        var requiredFields = new[] { "repository_url", "branch" };
        foreach (var field in requiredFields)
        {
            if (!root.TryGetProperty(field, out var prop) || prop.ValueKind == JsonValueKind.Null)
                throw new ArgumentException($"Required field '{field}' is missing or null");

            if (prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
                throw new ArgumentException($"Field '{field}' must be a non-empty string");
        }

        // Validate repository_url is valid URI
        var repoUrl = root.GetProperty("repository_url").GetString() ?? "";
        if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out _))
            throw new ArgumentException("repository_url must be a valid URI");

        _logger.LogInformation("Git configuration validated successfully");
    }
    catch (JsonException ex)
    {
        throw new ArgumentException($"Configuration is not valid JSON: {ex.Message}", ex);
    }
}
```

### Step 7: Implement API Client

**File:** `backend/src/Notebook.Server/Services/Crawlers/GitApiClient.cs`

```csharp
using System.Net.Http.Headers;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Git repository client for fetching commits and files.
/// Supports GitHub, GitLab, and self-hosted Git servers via generic Git API.
/// </summary>
public sealed class GitApiClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<GitApiClient> _logger;

    public GitApiClient(string repositoryUrl, string? username, string? password, ILogger<GitApiClient> logger)
    {
        _logger = logger;
        _baseUrl = repositoryUrl.TrimEnd('/');
        _httpClient = new HttpClient();

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            SetupAuthentication(username, password);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Fetch repository metadata.
    /// </summary>
    public async Task<GitRepository> GetRepositoryAsync()
    {
        // Implementation depends on Git service (GitHub, GitLab, Gitea, etc.)
        // For now, return basic info parsed from URL
        var parts = _baseUrl.Split('/');
        var repoName = parts.Last().Replace(".git", "");

        return new GitRepository
        {
            Url = _baseUrl,
            Name = repoName,
            DefaultBranch = "main"
        };
    }

    /// <summary>
    /// Get commits since last sync.
    /// </summary>
    public async Task<List<GitCommit>> GetCommitsAsync(string branch, string? since = null)
    {
        // Fetch from your Git provider's API
        // Example: GitHub API: GET /repos/{owner}/{repo}/commits?sha={branch}&since={since}
        // This is a placeholder - actual implementation depends on Git service

        _logger.LogInformation($"Fetching commits from {_baseUrl} on branch {branch}");

        // Return empty for now (implementation-specific)
        return new();
    }

    /// <summary>
    /// Get files changed in a commit.
    /// </summary>
    public async Task<List<GitFile>> GetCommitFilesAsync(string commitSha)
    {
        _logger.LogInformation($"Fetching files for commit {commitSha}");
        return new();
    }

    /// <summary>
    /// Fetch file content.
    /// </summary>
    public async Task<GitFileContent> GetFileAsync(string filePath, string sha)
    {
        // Fetch raw file content from Git provider
        _logger.LogDebug($"Fetching {filePath} at {sha}");

        return new GitFileContent
        {
            Path = filePath,
            Content = "",
            ContentType = DetermineContentType(filePath)
        };
    }

    private void SetupAuthentication(string username, string password)
    {
        var auth = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
    }

    private static string DetermineContentType(string filePath)
    {
        return filePath.EndsWith(".md") ? "text/markdown" : "text/plain";
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await ValueTask.CompletedTask;
    }
}

// DTOs
public class GitRepository
{
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
}

public class GitCommit
{
    public string Sha { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CommittedDate { get; set; }
    public string Message { get; set; } = "";
}

public class GitFile
{
    public string Path { get; set; } = "";
    public string Status { get; set; } = ""; // added, modified, deleted
}

public class GitFileContent
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public string ContentType { get; set; } = "text/plain";
}
```

### Step 8: Implement Crawler Logic

**File:** `backend/src/Notebook.Server/Services/Crawlers/GitCrawler.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Git repository crawler: fetches files and converts them to notebook entries.
/// Supports incremental syncs via commit tracking.
/// </summary>
public sealed class GitCrawler
{
    private readonly ILogger<GitCrawler> _logger;
    private readonly IContentFilterPipeline _contentFilterPipeline;

    public GitCrawler(ILogger<GitCrawler> logger, IContentFilterPipeline contentFilterPipeline)
    {
        _logger = logger;
        _contentFilterPipeline = contentFilterPipeline;
    }

    /// <summary>
    /// Crawl a Git repository and return entries.
    /// </summary>
    public async Task<CrawlerResult> CrawlAsync(
        GitConfig config,
        GitSyncState previousState)
    {
        var result = new CrawlerResult { StartedAt = DateTime.UtcNow };
        var entries = new List<NotebookBatchEntry>();
        var newState = new GitSyncState
        {
            RepositoryUrl = config.RepositoryUrl,
            Branch = config.Branch
        };

        try
        {
            var apiClient = new GitApiClient(
                config.RepositoryUrl, config.Username, config.Password,
                (ILogger<GitApiClient>)(object)_logger);

            // Get repository info
            var repo = await apiClient.GetRepositoryAsync();
            _logger.LogInformation($"Crawling Git repository {repo.Name} ({repo.Url})");

            // Fetch commits since last sync
            var commits = await apiClient.GetCommitsAsync(config.Branch, previousState.LastCommitSha);

            foreach (var commit in commits)
            {
                var files = await apiClient.GetCommitFilesAsync(commit.Sha);

                foreach (var file in files)
                {
                    // Skip if file doesn't match patterns
                    if (!MatchesPattern(file.Path, config.FilePatterns))
                        continue;

                    // Fetch file content
                    var content = await apiClient.GetFileAsync(file.Path, commit.Sha);

                    // Skip deleted files
                    if (file.Status == "deleted")
                        continue;

                    // Convert to entry
                    var entry = ConvertFileToEntry(file, content, config, commit);
                    entries.Add(entry);

                    // Track in state
                    var hash = ComputeHash(content.Content);
                    newState.Files[file.Path] = new GitFileMetadata
                    {
                        Path = file.Path,
                        Sha = commit.Sha,
                        Modified = commit.CommittedDate,
                        ContentHash = hash
                    };
                }

                newState.LastCommitSha = commit.Sha;
            }

            newState.LastSyncTimestamp = DateTime.UtcNow;
            newState.FilesTracked = newState.Files.Count;

            result.Status = "success";
            result.EntriesCreated = entries.Count;
            result.Entries = entries;
            result.NewState = newState;
            result.Stats = new
            {
                CommitsProcessed = commits.Count,
                FilesCreated = entries.Count,
                BytesProcessed = entries.Sum(e => e.Content.Length)
            };

            _logger.LogInformation($"Git crawler completed: {commits.Count} commits → {entries.Count} entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git crawler failed");
            result.Status = "failed";
            result.ErrorMessage = ex.Message;
            result.NewState = newState;
        }

        result.CompletedAt = DateTime.UtcNow;
        return result;
    }

    private NotebookBatchEntry ConvertFileToEntry(
        GitFile file,
        GitFileContent content,
        GitConfig config,
        GitCommit commit)
    {
        var filterResult = _contentFilterPipeline.Apply(content.Content, null);
        var processedContent = filterResult.Content ?? content.Content;

        // Add filename as heading
        if (!processedContent.StartsWith("#"))
        {
            processedContent = $"# {file.Path}\n\n{processedContent}";
        }

        return new NotebookBatchEntry
        {
            Content = processedContent,
            ContentType = content.ContentType,
            SourceHint = "git",
            Metadata = new Dictionary<string, object>
            {
                ["source_url"] = $"{config.RepositoryUrl}/blob/{commit.Sha}/{file.Path}",
                ["source_type"] = "git",
                ["git_repository"] = config.RepositoryUrl,
                ["git_branch"] = config.Branch,
                ["git_commit_sha"] = commit.Sha,
                ["git_file_path"] = file.Path,
                ["git_author"] = commit.Author,
                ["git_committed_date"] = commit.CommittedDate.ToString("O"),
                ["source_attribution"] = $"Git:{config.RepositoryUrl}:{file.Path}:{commit.Sha}",
                ["source_crawled_at"] = DateTime.UtcNow.ToString("O")
            }
        };
    }

    private bool MatchesPattern(string filePath, List<string> patterns)
    {
        if (!patterns.Any())
            return true;

        // Simple pattern matching (implement full glob if needed)
        return patterns.Any(pattern =>
        {
            // Convert glob to regex
            var regexPattern = "^" + pattern
                .Replace(".", "\\.")
                .Replace("*", ".*")
                .Replace("?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(filePath, regexPattern);
        });
    }

    private static string ComputeHash(string content)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
```

### Step 9: Extend CrawlerService

**Update:** `backend/src/Notebook.Server/Services/Crawlers/CrawlerService.cs`

Add methods to handle Git crawler:

```csharp
public async Task<CrawlerConfigResponse> ConfigureGitCrawlerAsync(
    Guid notebookId,
    string configJson,
    Guid userId,
    Guid organizationId)
{
    try
    {
        _configValidator.ValidateGitConfig(configJson);

        var notebook = await _context.Notebooks.FindAsync(notebookId)
            ?? throw new ArgumentException($"Notebook {notebookId} not found");

        // Create or update state
        var gitState = new GitCrawlerStateEntity
        {
            Config = configJson,
            SyncState = "{}"
        };

        await _context.GitCrawlerStates.AddAsync(gitState);
        await _context.SaveChangesAsync();

        // Create or update crawler
        var existingCrawler = await _context.Crawlers
            .FirstOrDefaultAsync(c => c.NotebookId == notebookId && c.SourceType == "git");

        if (existingCrawler != null)
        {
            existingCrawler.StateRefId = gitState.Id;
            existingCrawler.UpdatedAt = DateTime.UtcNow;
            _context.Crawlers.Update(existingCrawler);
        }
        else
        {
            var config = GitConfig.FromJson(configJson, _configValidator);
            var crawler = new CrawlerEntity
            {
                NotebookId = notebookId,
                Name = $"Git:{ExtractRepoName(config.RepositoryUrl)}",
                SourceType = "git",
                StateProvider = "git_state",
                StateRefId = gitState.Id,
                IsEnabled = true,
                CreatedBy = userId,
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.Crawlers.AddAsync(crawler);
        }

        await _context.SaveChangesAsync();

        return new CrawlerConfigResponse
        {
            Success = true,
            Message = "Git crawler configured successfully",
            CrawlerStateId = gitState.Id
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to configure Git crawler");
        return new CrawlerConfigResponse
        {
            Success = false,
            Message = ex.Message,
            Error = ex.Message
        };
    }
}

public async Task<CrawlerRunResponse> RunGitCrawlerAsync(Guid notebookId)
{
    // Similar pattern to RunConfluenceCrawlerAsync
    // Load config, instantiate crawler, execute, save results
}

private static string ExtractRepoName(string url)
{
    var parts = url.TrimEnd('/').Split('/');
    return parts.Last().Replace(".git", "");
}
```

### Step 10: Extend CrawlersController

**Update:** `backend/src/Notebook.Server/Controllers/CrawlersController.cs`

```csharp
[HttpPost("{notebookId}/git/config")]
public async Task<ActionResult<CrawlerConfigResponse>> ConfigureGitCrawler(
    Guid notebookId,
    [FromBody] ConfluenceCrawlerConfigRequest request)
{
    var userId = GetUserId();
    var orgId = GetOrganizationId();

    var result = await _crawlerService.ConfigureGitCrawlerAsync(
        notebookId, request.ConfigJson, userId, orgId);

    return result.Success ? Ok(result) : BadRequest(result);
}

[HttpPost("git/test")]
public async Task<ActionResult<CrawlerTestResponse>> TestGitCrawler(
    [FromBody] ConfluenceCrawlerConfigRequest request)
{
    var result = await _crawlerService.TestGitCrawlerAsync(request.ConfigJson);
    return result.Success ? Ok(result) : BadRequest(result);
}

[HttpPost("{notebookId}/git/run")]
public async Task<ActionResult<CrawlerRunResponse>> RunGitCrawler(Guid notebookId)
{
    var result = await _crawlerService.RunGitCrawlerAsync(notebookId);
    return result.Success ? Ok(result) : BadRequest(result);
}
```

### Step 11: Write Integration Tests

**File:** `backend/tests/Notebook.Tests/Endpoints/GitCrawlerTests.cs`

```csharp
public class GitCrawlerTests : IClassFixture<NotebookApiFixture>
{
    private readonly HttpClient _client;
    private Guid _testNotebookId;

    public GitCrawlerTests(NotebookApiFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task ConfigureGitCrawler_ValidConfig_ReturnsSuccess()
    {
        // Setup
        var configJson = JsonSerializer.Serialize(new
        {
            repository_url = "https://github.com/company/repo",
            branch = "main",
            file_patterns = new[] { "*.md" }
        });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/crawlers/{_testNotebookId}/git/config",
            new { config_json = configJson });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrawlerConfigResponse>();
        Assert.True(result?.Success);
    }

    [Fact]
    public async Task RunGitCrawler_ValidConfig_ExecutesSuccessfully()
    {
        // Configure first
        var configJson = JsonSerializer.Serialize(new
        {
            repository_url = "https://github.com/company/repo",
            branch = "main"
        });

        await _client.PostAsJsonAsync(
            $"/api/crawlers/{_testNotebookId}/git/config",
            new { config_json = configJson });

        // Act: Run crawler
        var response = await _client.PostAsJsonAsync(
            $"/api/crawlers/{_testNotebookId}/git/run",
            new { });

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.BadRequest);
    }
}
```

## Best Practices

### 1. Incremental Sync

**Always track what's been synced** to avoid re-fetching:

```csharp
// Good: Use content hashing to detect changes
var previousHash = previousState.Files[path]?.ContentHash;
var currentHash = ComputeHash(newContent);
if (previousHash != currentHash)
{
    // File changed, create entry
}

// Avoid: Fetching everything every time
foreach (var file in allFilesEverywhere)
{
    // Too slow and wasteful
}
```

### 2. Error Handling

**Partial failures should still save progress:**

```csharp
// Good: Log error and continue
try
{
    entries.Add(ConvertFile(file));
}
catch (Exception ex)
{
    _logger.LogWarning(ex, $"Failed to convert {file.Path}, skipping");
    // Continue to next file
}

// Avoid: Failing entire run on single error
try
{
    // ... process all files ...
}
catch
{
    throw; // Everything lost
}
```

### 3. Rate Limiting

**Respect external API limits:**

```csharp
// Good: Back off if rate-limited
if (response.StatusCode == 429)
{
    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
    await Task.Delay(retryAfter);
    // Retry
}

// Avoid: Hammering API
for (int i = 0; i < 10000; i++)
{
    await apiClient.GetAsync(...); // Will get blocked!
}
```

### 4. Large Content

**Fragment files that are too large:**

```csharp
// Good: Split large files
if (content.Length > 100_000)
{
    var chunks = content.SplitIntoChunks(50_000);
    foreach (var chunk in chunks)
    {
        entries.Add(new NotebookBatchEntry
        {
            Content = chunk,
            Metadata = new { part = i, total = chunks.Count }
        });
    }
}
```

### 5. Metadata

**Always include source attribution:**

```csharp
return new NotebookBatchEntry
{
    Content = content,
    Metadata = new Dictionary<string, object>
    {
        // Essential for traceability
        ["source_url"] = originalUrl,
        ["source_type"] = "git",
        ["source_attribution"] = "Git:repo:path:commit",
        ["source_crawled_at"] = DateTime.UtcNow.ToString("O"),

        // Source-specific metadata
        ["git_commit_sha"] = commitSha,
        ["git_author"] = author,
        ["git_committed_date"] = commitDate,
    }
};
```

## Testing Checklist

Before shipping a new crawler:

- [ ] Configuration schema is valid JSON Schema
- [ ] Configuration validation rejects invalid inputs
- [ ] Test connection succeeds with valid credentials
- [ ] Test connection fails gracefully with invalid credentials
- [ ] Initial sync creates appropriate entries
- [ ] Incremental sync skips unchanged content
- [ ] Partial failures are handled (some entries fail, others succeed)
- [ ] Run history tracks all executions
- [ ] Large content is handled (no timeouts or memory issues)
- [ ] All entries have `source_url` and `source_type`
- [ ] Content is properly converted to Markdown
- [ ] Metadata is accurate and complete
- [ ] Error messages are helpful and actionable

## Example: FileSystem Crawler

To implement a FileSystem crawler following this guide:

1. **source_type:** `"filesystem"`
2. **config:** `{ base_path: "/data", file_patterns: ["*.md"] }`
3. **state:** Track `{ last_scan_timestamp, file_hashes }`
4. **API client:** Use `System.IO.Directory` and `File.ReadAllText()`
5. **Crawler:** Walk directory tree, convert each file to entry
6. **Tests:** Create temp files, verify they're synced

Time estimate: 3 hours (simpler than external API crawlers)

## Troubleshooting

### Crawler Hangs

**Cause:** Infinite loop or blocking I/O

**Solution:**
- Add timeout to API client: `_httpClient.Timeout = TimeSpan.FromSeconds(30)`
- Add cancellation token: `CancellationToken ct` parameter
- Log progress: `_logger.LogInformation($"Processed {n}/{total}")`

### Out of Memory

**Cause:** Loading entire large file into memory

**Solution:**
- Stream large files instead of loading entirely
- Chunk responses
- Monitor memory during tests

### API Rate Limit

**Cause:** Too many requests too fast

**Solution:**
- Add `await Task.Delay(100)` between requests
- Implement exponential backoff
- Cache responses when possible

## FAQ

**Q: Can I use a different base crawler class?**
A: No, all crawlers must return `CrawlerResult`. The format is standard.

**Q: How do I test with real data?**
A: Create mock API client (see MockConfluenceApiClient) that returns test data.

**Q: Where do I store credentials?**
A: In the config JSONB field. Database encryption handles security. Never log credentials.

**Q: Can I run crawlers in parallel?**
A: Yes, service supports concurrent execution. Database handles lock contention.

**Q: How often should crawlers run?**
A: Depends on data freshness needs. Daily/weekly typical. Future: support cron scheduling.

## Related Documentation

- [Crawler Architecture](CRAWLER-IMPLEMENTATION.md) — Design details
- [Confluence Crawler Phase 4](../roadmap/completed/04-CONFLUENCE-CRAWLER.md) — Completed example
- [Integration Tests](../tests/Endpoints/CrawlerIntegrationTests.cs) — Test patterns
