using System.Net.Http.Headers;
using System.Text.Json;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// HttpClient wrapper for the Rust notebook API.
/// Adds X-Author-Id header automatically from the current user's AuthorId.
/// </summary>
public class NotebookApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotebookApiClient> _logger;

    public NotebookApiClient(HttpClient httpClient, ILogger<NotebookApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Register a new author with the Rust API.
    /// Called during user creation.
    /// </summary>
    public async Task<RegisterAuthorResponse?> RegisterAuthorAsync(string publicKeyHex)
    {
        var request = new RegisterAuthorRequest { PublicKey = publicKeyHex };
        var response = await _httpClient.PostAsJsonAsync("/authors", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegisterAuthorResponse>(JsonOptions);
    }

    /// <summary>
    /// List notebooks accessible to the given author.
    /// </summary>
    public async Task<ListNotebooksResponse?> ListNotebooksAsync(string authorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/notebooks");
        request.Headers.Add("X-Author-Id", authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListNotebooksResponse>(JsonOptions);
    }

    /// <summary>
    /// Create a new notebook.
    /// </summary>
    public async Task<CreateNotebookResponse?> CreateNotebookAsync(
        string authorIdHex, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/notebooks");
        request.Headers.Add("X-Author-Id", authorIdHex);
        request.Content = JsonContent.Create(new CreateNotebookRequest { Name = name }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateNotebookResponse>(JsonOptions);
    }

    /// <summary>
    /// Create a new entry in a notebook.
    /// </summary>
    public async Task<CreateEntryResponse?> CreateEntryAsync(
        string authorIdHex, Guid notebookId, CreateEntryRequest entry)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/notebooks/{notebookId}/entries");
        request.Headers.Add("X-Author-Id", authorIdHex);
        request.Content = JsonContent.Create(entry, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateEntryResponse>(JsonOptions);
    }

    /// <summary>
    /// Browse a notebook's catalog.
    /// </summary>
    public async Task<BrowseResponse?> BrowseAsync(
        string authorIdHex, Guid notebookId, string? query = null, int? maxTokens = null)
    {
        var url = $"/notebooks/{notebookId}/browse";
        var queryParams = new List<string>();
        if (query != null) queryParams.Add($"query={Uri.EscapeDataString(query)}");
        if (maxTokens.HasValue) queryParams.Add($"max_tokens={maxTokens.Value}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Author-Id", authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrowseResponse>(JsonOptions);
    }

    /// <summary>
    /// Observe changes in a notebook since a given sequence.
    /// </summary>
    public async Task<ObserveResponse?> ObserveAsync(
        string authorIdHex, Guid notebookId, ulong? since = null)
    {
        var url = $"/notebooks/{notebookId}/observe";
        if (since.HasValue) url += $"?since={since.Value}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Author-Id", authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ObserveResponse>(JsonOptions);
    }
}
