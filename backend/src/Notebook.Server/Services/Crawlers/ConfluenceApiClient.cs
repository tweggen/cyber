using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notebook.Server.Services.Crawlers;

/// <summary>
/// Confluence REST API client for page fetching and metadata retrieval.
/// Handles authentication, pagination, and response parsing.
/// </summary>
public sealed class ConfluenceApiClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<ConfluenceApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConfluenceApiClient(string baseUrl, string username, string apiToken, ILogger<ConfluenceApiClient> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;

        _httpClient = new HttpClient();
        SetupAuthentication(username, apiToken);
    }

    /// <summary>
    /// Fetch space information.
    /// </summary>
    public async Task<ConfluenceSpace> GetSpaceAsync(string spaceKey)
    {
        var url = $"{_baseUrl}/rest/api/v3/spaces?keys={Uri.EscapeDataString(spaceKey)}";
        var response = await GetAsync(url);

        using var doc = JsonDocument.Parse(response);
        var space = doc.RootElement.GetProperty("results")[0];

        return new ConfluenceSpace
        {
            Id = space.GetProperty("id").GetInt64(),
            Key = space.GetProperty("key").GetString() ?? "",
            Name = space.GetProperty("name").GetString() ?? ""
        };
    }

    /// <summary>
    /// Fetch pages from a space with pagination, filtering, and incremental sync support.
    /// </summary>
    public async Task<(List<ConfluencePage> Pages, string? NextCursor)> GetPagesAsync(
        string spaceKey,
        int limit = 25,
        string? cursor = null,
        List<string>? includeLabels = null,
        List<string>? excludeLabels = null)
    {
        var url = $"{_baseUrl}/rest/api/v3/spaces/{Uri.EscapeDataString(spaceKey)}/pages" +
                  $"?status=current,draft" +
                  $"&expand=body.view,version,metadata.labels" +
                  $"&limit={limit}";

        if (!string.IsNullOrEmpty(cursor))
            url += $"&cursor={Uri.EscapeDataString(cursor)}";

        var response = await GetAsync(url);
        using var doc = JsonDocument.Parse(response);

        var pages = new List<ConfluencePage>();
        var results = doc.RootElement.GetProperty("results");

        foreach (var pageElem in results.EnumerateArray())
        {
            var page = ParsePage(pageElem);

            // Apply label filters
            if (ShouldIncludePage(page, includeLabels, excludeLabels))
            {
                pages.Add(page);
            }
        }

        // Check for next cursor
        string? nextCursor = null;
        if (doc.RootElement.TryGetProperty("_links", out var links) &&
            links.TryGetProperty("next", out var next))
        {
            nextCursor = next.GetString();
        }

        return (pages, nextCursor);
    }

    /// <summary>
    /// Fetch a single page with full body content.
    /// </summary>
    public async Task<ConfluencePage> GetPageAsync(string pageId)
    {
        var url = $"{_baseUrl}/rest/api/v3/pages/{Uri.EscapeDataString(pageId)}" +
                  $"?expand=body.view,version,metadata.labels";

        var response = await GetAsync(url);
        using var doc = JsonDocument.Parse(response);

        return ParsePage(doc.RootElement);
    }

    /// <summary>
    /// Fetch page attachments.
    /// </summary>
    public async Task<List<ConfluenceAttachment>> GetPageAttachmentsAsync(string pageId)
    {
        var url = $"{_baseUrl}/rest/api/v3/pages/{Uri.EscapeDataString(pageId)}/attachments";
        var response = await GetAsync(url);
        using var doc = JsonDocument.Parse(response);

        var attachments = new List<ConfluenceAttachment>();
        var results = doc.RootElement.GetProperty("results");

        foreach (var attachElem in results.EnumerateArray())
        {
            attachments.Add(new ConfluenceAttachment
            {
                Id = attachElem.GetProperty("id").GetString() ?? "",
                Title = attachElem.GetProperty("title").GetString() ?? "",
                MediaType = attachElem.GetProperty("mediaType").GetString() ?? "",
                FileSize = attachElem.TryGetProperty("fileSize", out var size) ? size.GetInt64() : 0,
                DownloadUrl = attachElem.GetProperty("_links").GetProperty("download").GetString() ?? ""
            });
        }

        return attachments;
    }

    /// <summary>
    /// Private helper: Make authenticated HTTP GET request.
    /// </summary>
    private async Task<string> GetAsync(string url)
    {
        _logger.LogDebug($"GET {url}");
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Confluence API error {response.StatusCode}: {error}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Setup HTTP Basic authentication.
    /// </summary>
    private void SetupAuthentication(string username, string apiToken)
    {
        var auth = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Parse a page from JSON response.
    /// </summary>
    private static ConfluencePage ParsePage(JsonElement pageElem)
    {
        var labels = new List<string>();
        if (pageElem.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("labels", out var labelArray))
        {
            foreach (var label in labelArray.GetProperty("results").EnumerateArray())
            {
                labels.Add(label.GetProperty("name").GetString() ?? "");
            }
        }

        return new ConfluencePage
        {
            Id = pageElem.GetProperty("id").GetString() ?? "",
            Title = pageElem.GetProperty("title").GetString() ?? "",
            Status = pageElem.GetProperty("status").GetString() ?? "current",
            HtmlBody = pageElem.TryGetProperty("body", out var body) &&
                       body.TryGetProperty("view", out var view)
                ? view.GetProperty("value").GetString() ?? ""
                : "",
            Version = new ConfluenceVersion
            {
                Number = pageElem.GetProperty("version").GetProperty("number").GetInt32(),
                CreatedAt = DateTime.Parse(pageElem.GetProperty("version").GetProperty("createdAt").GetString() ?? "")
            },
            WebUrl = pageElem.GetProperty("_links").GetProperty("webui").GetString() ?? "",
            Labels = labels
        };
    }

    /// <summary>
    /// Check if page should be included based on label filters.
    /// </summary>
    private static bool ShouldIncludePage(
        ConfluencePage page,
        List<string>? includeLabels,
        List<string>? excludeLabels)
    {
        // If exclude labels specified, skip if any match
        if (excludeLabels?.Count > 0)
        {
            foreach (var excludeLabel in excludeLabels)
            {
                if (page.Labels.Contains(excludeLabel, StringComparer.OrdinalIgnoreCase))
                    return false;
            }
        }

        // If include labels specified, skip if none match
        if (includeLabels?.Count > 0)
        {
            var hasIncludeLabel = includeLabels.Any(label =>
                page.Labels.Contains(label, StringComparer.OrdinalIgnoreCase));
            if (!hasIncludeLabel)
                return false;
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        await ValueTask.CompletedTask;
    }
}

// ============= DTOs =============

public class ConfluenceSpace
{
    public long Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public class ConfluencePage
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "current";
    public string HtmlBody { get; set; } = "";
    public ConfluenceVersion Version { get; set; } = new();
    public string WebUrl { get; set; } = "";
    public List<string> Labels { get; set; } = new();
}

public class ConfluenceVersion
{
    public int Number { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ConfluenceAttachment
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string MediaType { get; set; } = "";
    public long FileSize { get; set; }
    public string DownloadUrl { get; set; } = "";
}
