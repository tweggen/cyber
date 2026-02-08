using System.Net.Http.Headers;
using System.Text.Json;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// HttpClient wrapper for the Rust notebook API.
/// Authenticates via JWT Bearer tokens signed by TokenService.
/// </summary>
public class NotebookApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<NotebookApiClient> _logger;

    public NotebookApiClient(
        HttpClient httpClient,
        TokenService tokenService,
        ILogger<NotebookApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Register a new author with the Rust API.
    /// Called during user creation. No auth needed for author registration.
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
        AddAuthHeader(request, authorIdHex);
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
        AddAuthHeader(request, authorIdHex);
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
        AddAuthHeader(request, authorIdHex);
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
        AddAuthHeader(request, authorIdHex);
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
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ObserveResponse>(JsonOptions);
    }

    /// <summary>
    /// Read a specific entry with metadata, revisions, and references.
    /// </summary>
    public async Task<ReadEntryResponse?> ReadEntryAsync(
        string authorIdHex, Guid notebookId, Guid entryId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/entries/{entryId}");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReadEntryResponse>(JsonOptions);
    }

    /// <summary>
    /// Revise an existing entry.
    /// </summary>
    public async Task<ReviseEntryResponse?> ReviseEntryAsync(
        string authorIdHex, Guid notebookId, Guid entryId, ReviseEntryRequest reviseRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/notebooks/{notebookId}/entries/{entryId}");
        AddAuthHeader(request, authorIdHex);
        request.Content = JsonContent.Create(reviseRequest, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReviseEntryResponse>(JsonOptions);
    }

    /// <summary>
    /// Share a notebook with another author.
    /// </summary>
    public async Task<ShareResponse?> ShareNotebookAsync(
        string authorIdHex, Guid notebookId, ShareRequest shareRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/share");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(shareRequest, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareResponse>(JsonOptions);
    }

    /// <summary>
    /// Revoke a shared author's access to a notebook.
    /// </summary>
    public async Task<RevokeResponse?> RevokeShareAsync(
        string authorIdHex, Guid notebookId, string targetAuthorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/notebooks/{notebookId}/share/{targetAuthorIdHex}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RevokeResponse>(JsonOptions);
    }

    /// <summary>
    /// List all participants with access to a notebook.
    /// </summary>
    public async Task<ParticipantsResponse?> ListParticipantsAsync(
        string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/notebooks/{notebookId}/participants");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ParticipantsResponse>(JsonOptions);
    }

    /// <summary>
    /// Delete a notebook (owner only).
    /// </summary>
    public async Task<DeleteNotebookResponse?> DeleteNotebookAsync(
        string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/notebooks/{notebookId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeleteNotebookResponse>(JsonOptions);
    }

    /// <summary>
    /// Add JWT Bearer token to the request for the given author.
    /// </summary>
    private void AddAuthHeader(HttpRequestMessage request, string authorIdHex, bool admin = false)
    {
        var token = admin
            ? _tokenService.GenerateAdminToken(authorIdHex)
            : _tokenService.GenerateToken(authorIdHex);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
