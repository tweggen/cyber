using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/search", Search)
            .RequireAuthorization();
        routes.MapPost("/notebooks/{notebookId}/semantic-search", SemanticSearch)
            .RequireAuthorization();
        routes.MapPost("/notebooks/{notebookId}/claims/batch", ClaimsBatch)
            .RequireAuthorization();
    }

    private static async Task<IResult> Search(
        Guid notebookId,
        [FromQuery] string query,
        [FromQuery(Name = "search_in")] string searchIn = "both",
        [FromQuery(Name = "topic_prefix")] string? topicPrefix = null,
        [FromQuery(Name = "max_results")] int maxResults = 20,
        IEntryRepository entryRepo = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Results.BadRequest(new { error = "query is required" });

        maxResults = Math.Min(maxResults, 100);

        var results = await entryRepo.SearchEntriesAsync(
            notebookId, query, searchIn, topicPrefix, maxResults, ct);

        return Results.Ok(new SearchResponse { Results = results });
    }

    private static async Task<IResult> SemanticSearch(
        Guid notebookId,
        [FromBody] SemanticSearchRequest request,
        IEntryRepository entryRepo,
        IEmbeddingService embeddingService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return Results.BadRequest(new { error = "query is required" });

        var topK = Math.Min(Math.Max(request.TopK, 1), 100);
        var minSimilarity = Math.Max(request.MinSimilarity, 0.0);

        double[] queryEmbedding;
        try
        {
            queryEmbedding = await embeddingService.EmbedQueryAsync(request.Query, ct);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: $"Embedding service unavailable: {ex.Message}",
                statusCode: 503);
        }

        var results = await entryRepo.SemanticSearchAsync(
            notebookId, queryEmbedding, topK, minSimilarity, ct);

        return Results.Ok(new SemanticSearchResponse { Results = results });
    }

    private static async Task<IResult> ClaimsBatch(
        Guid notebookId,
        [FromBody] ClaimsBatchRequest request,
        IEntryRepository entryRepo,
        CancellationToken ct)
    {
        if (request.EntryIds.Count == 0)
            return Results.BadRequest(new { error = "entry_ids is required" });

        if (request.EntryIds.Count > 100)
            return Results.BadRequest(new { error = "max 100 entry_ids per request" });

        var entries = await entryRepo.GetClaimsBatchAsync(notebookId, request.EntryIds, ct);

        return Results.Ok(new ClaimsBatchResponse { Entries = entries });
    }
}
