using System.Net.Http.Headers;
using System.Text.Json;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// HttpClient wrapper for the notebook API.
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
    /// Register a new author with the notebook API.
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
    /// Browse a notebook's entries with optional filters.
    /// </summary>
    public async Task<BrowseFilteredResponse?> BrowseFilteredAsync(
        string authorIdHex,
        Guid notebookId,
        string? topicPrefix = null,
        string? claimsStatus = null,
        string? author = null,
        long? sequenceMin = null,
        long? sequenceMax = null,
        double? hasFrictionAbove = null,
        bool? needsReview = null,
        string? integrationStatus = null,
        int? limit = null,
        int? offset = null)
    {
        var url = $"/notebooks/{notebookId}/browse";
        var queryParams = new List<string>();

        if (topicPrefix != null) queryParams.Add($"topic_prefix={Uri.EscapeDataString(topicPrefix)}");
        if (claimsStatus != null) queryParams.Add($"claims_status={claimsStatus}");
        if (author != null) queryParams.Add($"author={author}");
        if (sequenceMin.HasValue) queryParams.Add($"sequence_min={sequenceMin.Value}");
        if (sequenceMax.HasValue) queryParams.Add($"sequence_max={sequenceMax.Value}");
        if (hasFrictionAbove.HasValue) queryParams.Add($"has_friction_above={hasFrictionAbove.Value}");
        if (needsReview.HasValue) queryParams.Add($"needs_review={needsReview.Value}");
        if (integrationStatus != null) queryParams.Add($"integration_status={integrationStatus}");
        if (limit.HasValue) queryParams.Add($"limit={limit.Value}");
        if (offset.HasValue) queryParams.Add($"offset={offset.Value}");

        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrowseFilteredResponse>(JsonOptions);
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
    /// Rename a notebook (owner only).
    /// </summary>
    public async Task<RenameNotebookResponse?> RenameNotebookAsync(
        string authorIdHex, Guid notebookId, string newName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/notebooks/{notebookId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(
            new RenameNotebookRequest { Name = newName }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RenameNotebookResponse>(JsonOptions);
    }

    /// <summary>
    /// Reset all failed jobs back to pending (owner only).
    /// </summary>
    public async Task<int> RetryFailedJobsAsync(string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/jobs/retry-failed");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("retried").GetInt32();
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

    // =========================================================================
    // Organizations & Groups
    // =========================================================================

    public async Task<ListOrganizationsResponse?> ListOrganizationsAsync(string authorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/organizations");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListOrganizationsResponse>(JsonOptions);
    }

    public async Task<OrganizationResponse?> CreateOrganizationAsync(string authorIdHex, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/organizations");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new CreateOrganizationRequest { Name = name }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrganizationResponse>(JsonOptions);
    }

    public async Task<ListGroupsResponse?> ListGroupsAsync(string authorIdHex, Guid orgId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/organizations/{orgId}/groups");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListGroupsResponse>(JsonOptions);
    }

    public async Task<GroupResponse?> CreateGroupAsync(string authorIdHex, Guid orgId, string name, Guid? parentId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/organizations/{orgId}/groups");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new CreateGroupRequest { Name = name, ParentId = parentId }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GroupResponse>(JsonOptions);
    }

    public async Task DeleteGroupAsync(string authorIdHex, Guid groupId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/groups/{groupId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddEdgeAsync(string authorIdHex, Guid orgId, Guid parentId, Guid childId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/organizations/{orgId}/edges");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new AddEdgeRequest { ParentId = parentId, ChildId = childId }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveEdgeAsync(string authorIdHex, Guid parentId, Guid childId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/groups/{parentId}/edges/{childId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ListMembersResponse?> ListMembersAsync(string authorIdHex, Guid groupId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/groups/{groupId}/members");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListMembersResponse>(JsonOptions);
    }

    public async Task AddMemberAsync(string authorIdHex, Guid groupId, string memberAuthorIdHex, string role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/members");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new AddMemberRequest { AuthorId = memberAuthorIdHex, Role = role }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveMemberAsync(string authorIdHex, Guid groupId, string memberAuthorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/groups/{groupId}/members/{memberAuthorIdHex}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignNotebookToGroupAsync(string authorIdHex, Guid notebookId, Guid? groupId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/notebooks/{notebookId}/group");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new AssignGroupRequest { GroupId = groupId }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Clearances
    // =========================================================================

    public async Task<ClearanceSummaryResponse?> GrantClearanceAsync(
        string authorIdHex, string targetAuthorIdHex, Guid orgId, string maxLevel, List<string>? compartments = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/clearances");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new GrantClearanceRequest
        {
            AuthorId = targetAuthorIdHex,
            OrganizationId = orgId,
            MaxLevel = maxLevel,
            Compartments = compartments ?? [],
        }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClearanceSummaryResponse>(JsonOptions);
    }

    public async Task RevokeClearanceAsync(string authorIdHex, string targetAuthorIdHex, Guid orgId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/clearances/{targetAuthorIdHex}/{orgId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ListClearancesResponse?> ListClearancesAsync(string authorIdHex, Guid orgId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/organizations/{orgId}/clearances");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListClearancesResponse>(JsonOptions);
    }

    public async Task FlushClearanceCacheAsync(string authorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/cache/flush");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Reviews
    // =========================================================================

    public async Task<ListReviewsResponse?> ListReviewsAsync(
        string authorIdHex, Guid notebookId, string? status = null)
    {
        var url = $"/notebooks/{notebookId}/reviews";
        if (status != null) url += $"?status={Uri.EscapeDataString(status)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListReviewsResponse>(JsonOptions);
    }

    public async Task ApproveReviewAsync(string authorIdHex, Guid notebookId, Guid reviewId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/reviews/{reviewId}/approve");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectReviewAsync(string authorIdHex, Guid notebookId, Guid reviewId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/reviews/{reviewId}/reject");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Agents
    // =========================================================================

    public async Task<ListAgentsResponse?> ListAgentsAsync(string authorIdHex)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/agents");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListAgentsResponse>(JsonOptions);
    }

    public async Task<AgentResponse?> RegisterAgentAsync(string authorIdHex, RegisterAgentRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/agents");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
    }

    public async Task<AgentResponse?> UpdateAgentAsync(string authorIdHex, string agentId, UpdateAgentRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/agents/{Uri.EscapeDataString(agentId)}");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
    }

    public async Task DeleteAgentAsync(string authorIdHex, string agentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/agents/{Uri.EscapeDataString(agentId)}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Subscriptions
    // =========================================================================

    public async Task<ListSubscriptionsResponse?> ListSubscriptionsAsync(string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/subscriptions");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ListSubscriptionsResponse>(JsonOptions);
    }

    public async Task<SubscriptionResponse?> CreateSubscriptionAsync(
        string authorIdHex, Guid notebookId, CreateSubscriptionRequest body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/notebooks/{notebookId}/subscriptions");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubscriptionResponse>(JsonOptions);
    }

    public async Task TriggerSyncAsync(string authorIdHex, Guid notebookId, Guid subId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/subscriptions/{subId}/sync");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSubscriptionAsync(string authorIdHex, Guid notebookId, Guid subId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/notebooks/{notebookId}/subscriptions/{subId}");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Audit
    // =========================================================================

    public async Task<AuditResponseDto?> QueryNotebookAuditAsync(
        string authorIdHex, Guid notebookId, string? action = null, int limit = 50, long? before = null)
    {
        var url = $"/notebooks/{notebookId}/audit?limit={limit}";
        if (action != null) url += $"&action={Uri.EscapeDataString(action)}";
        if (before.HasValue) url += $"&before={before.Value}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuditResponseDto>(JsonOptions);
    }

    public async Task<AuditResponseDto?> QueryGlobalAuditAsync(
        string authorIdHex, string? actor = null, string? action = null,
        string? resource = null, DateTimeOffset? from = null, DateTimeOffset? to = null,
        int limit = 100, long? before = null)
    {
        var parts = new List<string> { $"limit={limit}" };
        if (actor != null) parts.Add($"actor={Uri.EscapeDataString(actor)}");
        if (action != null) parts.Add($"action={Uri.EscapeDataString(action)}");
        if (resource != null) parts.Add($"resource={Uri.EscapeDataString(resource)}");
        if (from.HasValue) parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue) parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        if (before.HasValue) parts.Add($"before={before.Value}");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/audit?" + string.Join("&", parts));
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuditResponseDto>(JsonOptions);
    }

    // =========================================================================
    // Search
    // =========================================================================

    public async Task<LexicalSearchResponse?> ServerSearchAsync(
        string authorIdHex, Guid notebookId, string query,
        string searchIn = "both", int maxResults = 20)
    {
        var url = $"/notebooks/{notebookId}/search?query={Uri.EscapeDataString(query)}"
                  + $"&search_in={Uri.EscapeDataString(searchIn)}&max_results={maxResults}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LexicalSearchResponse>(JsonOptions);
    }

    // =========================================================================
    // Job Stats
    // =========================================================================

    public async Task<JobStatsResponse?> QueryJobStatsAsync(string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/notebooks/{notebookId}/jobs/stats");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobStatsResponse>(JsonOptions);
    }

    // =========================================================================
    // Batch Operations
    // =========================================================================

    /// <summary>
    /// Create multiple entries in a single batch (max 100).
    /// </summary>
    public async Task<BatchWriteResponse?> BatchWriteAsync(
        string authorIdHex, Guid notebookId, BatchWriteRequest batchRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/notebooks/{notebookId}/batch");
        AddAuthHeader(request, authorIdHex);
        request.Content = JsonContent.Create(batchRequest, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BatchWriteResponse>(JsonOptions);
    }

    // =========================================================================
    // Crawlers
    // =========================================================================

    /// <summary>
    /// Configure or update a Confluence crawler for a notebook.
    /// </summary>
    public async Task<CrawlerConfigResponse?> ConfigureConfluenceCrawlerAsync(
        string authorIdHex, Guid notebookId, string configJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/crawlers/{notebookId}/confluence/config");
        AddAuthHeader(request, authorIdHex, admin: true);
        request.Content = JsonContent.Create(new { config_json = configJson }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerConfigResponse>(JsonOptions);
    }

    /// <summary>
    /// Test a Confluence crawler connection with the given configuration.
    /// </summary>
    public async Task<CrawlerTestResponse?> TestConfluenceCrawlerAsync(
        string authorIdHex, string configJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/crawlers/confluence/test");
        AddAuthHeader(request, authorIdHex);
        request.Content = JsonContent.Create(new { config_json = configJson }, options: JsonOptions);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerTestResponse>(JsonOptions);
    }

    /// <summary>
    /// Execute a crawler run for a notebook.
    /// </summary>
    public async Task<CrawlerRunResponse?> RunConfluenceCrawlerAsync(
        string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/crawlers/{notebookId}/confluence/run");
        AddAuthHeader(request, authorIdHex, admin: true);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerRunResponse>(JsonOptions);
    }

    /// <summary>
    /// Get the current crawler configuration for a notebook.
    /// </summary>
    public async Task<CrawlerConfigResponse?> GetCrawlerConfigAsync(
        string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/crawlers/{notebookId}/confluence/config");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerConfigResponse>(JsonOptions);
    }

    /// <summary>
    /// Get the run history for a crawler.
    /// </summary>
    public async Task<List<CrawlerRunHistory>?> GetCrawlerRunsAsync(
        string authorIdHex, Guid notebookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/crawlers/{notebookId}/runs");
        AddAuthHeader(request, authorIdHex);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CrawlerRunHistory>>(JsonOptions);
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
