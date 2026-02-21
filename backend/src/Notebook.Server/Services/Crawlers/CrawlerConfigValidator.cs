using System.Text.Json;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Validates crawler configurations.
/// Uses manual validation of required fields and basic type checking.
/// Full JSON schema validation can be added with JsonSchema.Net in Phase 2.
/// </summary>
public sealed class CrawlerConfigValidator
{
    private readonly ILogger<CrawlerConfigValidator> _logger;

    public CrawlerConfigValidator(ILogger<CrawlerConfigValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate Confluence crawler configuration.
    /// Checks for required fields: base_url, username, api_token, space_key.
    /// </summary>
    /// <exception cref="ArgumentException">If configuration is invalid.</exception>
    public void ValidateConfluenceConfig(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            // Check for required fields
            var requiredFields = new[] { "base_url", "username", "api_token", "space_key" };
            foreach (var field in requiredFields)
            {
                if (!root.TryGetProperty(field, out var prop) || prop.ValueKind == JsonValueKind.Null)
                    throw new ArgumentException($"Required field '{field}' is missing or null");

                if (prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
                    throw new ArgumentException($"Field '{field}' must be a non-empty string");
            }

            // Validate space_key format (should be uppercase alphanumeric)
            var spaceKey = root.GetProperty("space_key").GetString() ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(spaceKey, @"^[A-Z][A-Z0-9]*$"))
                throw new ArgumentException("space_key must start with uppercase letter and contain only uppercase letters and digits");

            // Validate optional fields
            if (root.TryGetProperty("max_pages", out var maxPages) && maxPages.ValueKind != JsonValueKind.Null)
            {
                if (maxPages.ValueKind != JsonValueKind.Number || maxPages.GetInt32() < 0)
                    throw new ArgumentException("max_pages must be a non-negative integer");
            }

            if (root.TryGetProperty("include_labels", out var include) && include.ValueKind != JsonValueKind.Null)
            {
                if (include.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException("include_labels must be an array of strings");
            }

            if (root.TryGetProperty("exclude_labels", out var exclude) && exclude.ValueKind != JsonValueKind.Null)
            {
                if (exclude.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException("exclude_labels must be an array of strings");
            }

            _logger.LogInformation("Confluence configuration validated successfully");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Configuration is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate Confluence crawler sync state (lenient, internal use).
    /// Doesn't throw — sync state may be incomplete.
    /// </summary>
    public void ValidateConfluenceSyncState(string syncStateJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(syncStateJson);
            // Just parse to ensure it's valid JSON
            _logger.LogInformation("Confluence sync state is valid JSON");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning($"Could not parse sync state JSON: {ex.Message}");
            // Don't throw — sync state may be partially initialized
        }
    }
}

/// <summary>
/// Parsed and validated Confluence crawler configuration.
/// </summary>
public sealed class ConfluenceConfig
{
    public required string BaseUrl { get; init; }
    public required string Username { get; init; }
    public required string ApiToken { get; init; }
    public required string SpaceKey { get; init; }
    public List<string> IncludeLabels { get; init; } = new();
    public List<string> ExcludeLabels { get; init; } = new();
    public int MaxPages { get; init; } = 0;
    public bool IncludeAttachments { get; init; } = false;

    /// <summary>
    /// Parse and validate a configuration from JSON string.
    /// </summary>
    public static ConfluenceConfig FromJson(string json, CrawlerConfigValidator validator)
    {
        validator.ValidateConfluenceConfig(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new ConfluenceConfig
        {
            BaseUrl = root.GetProperty("base_url").GetString() ?? throw new InvalidOperationException("base_url is required"),
            Username = root.GetProperty("username").GetString() ?? throw new InvalidOperationException("username is required"),
            ApiToken = root.GetProperty("api_token").GetString() ?? throw new InvalidOperationException("api_token is required"),
            SpaceKey = root.GetProperty("space_key").GetString() ?? throw new InvalidOperationException("space_key is required"),
            IncludeLabels = root.TryGetProperty("include_labels", out var labels)
                ? labels.EnumerateArray().Select(l => l.GetString() ?? "").ToList()
                : new(),
            ExcludeLabels = root.TryGetProperty("exclude_labels", out var exclude)
                ? exclude.EnumerateArray().Select(l => l.GetString() ?? "").ToList()
                : new(),
            MaxPages = root.TryGetProperty("max_pages", out var max) ? max.GetInt32() : 0,
            IncludeAttachments = root.TryGetProperty("include_attachments", out var attach) && attach.GetBoolean()
        };
    }
}

/// <summary>
/// Confluence crawler sync state for incremental updates.
/// </summary>
public sealed class ConfluenceSyncState
{
    public string SpaceKey { get; set; } = "";
    public long? SpaceId { get; set; }
    public DateTime LastSyncTimestamp { get; set; }
    public int PagesSynced { get; set; }
    public Dictionary<string, ConfluencePageMetadata> PageMetadata { get; set; } = new();

    /// <summary>
    /// Convert to JSON for storage.
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Parse from JSON.
    /// </summary>
    public static ConfluenceSyncState FromJson(string json, CrawlerConfigValidator validator)
    {
        validator.ValidateConfluenceSyncState(json);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<ConfluenceSyncState>(json, options)
            ?? new ConfluenceSyncState();
    }
}

/// <summary>
/// Metadata about a single Confluence page (for incremental sync).
/// </summary>
public sealed class ConfluencePageMetadata
{
    public string Title { get; set; } = "";
    public int Version { get; set; }
    public DateTime LastModified { get; set; }
    public string Status { get; set; } = "current";
    public string ContentHash { get; set; } = "";
}
