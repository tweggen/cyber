using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;

namespace Notebook.Server.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/search", Search)
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
}
