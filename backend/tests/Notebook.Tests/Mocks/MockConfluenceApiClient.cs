using Notebook.Server.Services.Crawlers;

namespace Notebook.Tests.Mocks;

/// <summary>
/// Mock Confluence API client for testing. Returns predictable test data without
/// making real API calls to Confluence.
/// </summary>
public class MockConfluenceApiClient : IAsyncDisposable
{
    private readonly Dictionary<string, ConfluenceSpace> _spaces = new();
    private readonly Dictionary<string, List<ConfluencePage>> _pagesBySpace = new();
    private bool _shouldFailGetSpace = false;
    private bool _shouldFailGetPages = false;
    private string? _failureMessage;

    /// <summary>
    /// Add a mock space to the API response.
    /// </summary>
    public void AddMockSpace(string spaceKey, ConfluenceSpace space)
    {
        _spaces[spaceKey] = space;
    }

    /// <summary>
    /// Add mock pages for a space.
    /// </summary>
    public void AddMockPages(string spaceKey, List<ConfluencePage> pages)
    {
        _pagesBySpace[spaceKey] = pages;
    }

    /// <summary>
    /// Configure the mock to fail on GetSpaceAsync.
    /// </summary>
    public void SetFailureMode(string errorMessage)
    {
        _failureMessage = errorMessage;
    }

    /// <summary>
    /// Configure the mock to fail specifically on GetSpaceAsync.
    /// </summary>
    public void SetFailureOnGetSpace(bool fail = true)
    {
        _shouldFailGetSpace = fail;
    }

    /// <summary>
    /// Configure the mock to fail specifically on GetPagesAsync.
    /// </summary>
    public void SetFailureOnGetPages(bool fail = true)
    {
        _shouldFailGetPages = fail;
    }

    /// <summary>
    /// Mock implementation of GetSpaceAsync.
    /// </summary>
    public Task<ConfluenceSpace> GetSpaceAsync(string spaceKey)
    {
        if (_shouldFailGetSpace)
        {
            throw new HttpRequestException(
                _failureMessage ?? "Mock space fetch failed",
                null,
                System.Net.HttpStatusCode.Unauthorized);
        }

        if (!_spaces.TryGetValue(spaceKey, out var space))
        {
            throw new HttpRequestException(
                $"Space '{spaceKey}' not found",
                null,
                System.Net.HttpStatusCode.NotFound);
        }

        return Task.FromResult(space);
    }

    /// <summary>
    /// Mock implementation of GetPagesAsync with pagination and label filtering support.
    /// </summary>
    public Task<(List<ConfluencePage> Pages, string? NextCursor)> GetPagesAsync(
        string spaceKey,
        int limit = 25,
        string? cursor = null,
        List<string>? includeLabels = null,
        List<string>? excludeLabels = null)
    {
        if (_shouldFailGetPages)
        {
            throw new HttpRequestException(
                _failureMessage ?? "Mock pages fetch failed",
                null,
                System.Net.HttpStatusCode.InternalServerError);
        }

        if (!_pagesBySpace.TryGetValue(spaceKey, out var allPages))
        {
            return Task.FromResult((new List<ConfluencePage>(), (string?)null));
        }

        // Apply label filters
        var filteredPages = allPages.Where(p =>
            ShouldIncludePage(p, includeLabels, excludeLabels)).ToList();

        // Simple pagination: parse cursor as page number
        var pageNumber = 0;
        if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out var cursorPage))
        {
            pageNumber = cursorPage;
        }

        var startIndex = pageNumber * limit;
        var pages = filteredPages.Skip(startIndex).Take(limit).ToList();

        // Determine if there's a next page
        string? nextCursor = null;
        if (startIndex + limit < filteredPages.Count)
        {
            nextCursor = (pageNumber + 1).ToString();
        }

        return Task.FromResult((pages, nextCursor));
    }

    /// <summary>
    /// Mock implementation of GetPageAsync.
    /// </summary>
    public Task<ConfluencePage> GetPageAsync(string pageId)
    {
        foreach (var pages in _pagesBySpace.Values)
        {
            var page = pages.FirstOrDefault(p => p.Id == pageId);
            if (page != null)
            {
                return Task.FromResult(page);
            }
        }

        throw new HttpRequestException(
            $"Page '{pageId}' not found",
            null,
            System.Net.HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Mock implementation of GetPageAttachmentsAsync.
    /// </summary>
    public Task<List<ConfluenceAttachment>> GetPageAttachmentsAsync(string pageId)
    {
        // Always return empty list for now (attachments not tested in phase 5)
        return Task.FromResult(new List<ConfluenceAttachment>());
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
        await ValueTask.CompletedTask;
    }
}
