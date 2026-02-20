using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;
using Notebook.Server.Filters;

namespace Notebook.Server.Endpoints;

public static class BrowseEndpoints
{
    public static void MapBrowseEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/browse", Browse)
            .AddEndpointFilter<NotebookAccessFilter>()
            .RequireAuthorization("CanRead");
    }

    private static async Task<IResult> Browse(
        Guid notebookId,
        [FromQuery] string? query,
        [FromQuery(Name = "max_entries")] int? maxEntries,
        [FromQuery(Name = "topic_prefix")] string? topicPrefix,
        [FromQuery(Name = "claims_status")] string? claimsStatus,
        [FromQuery] string? author,
        [FromQuery(Name = "sequence_min")] long? sequenceMin,
        [FromQuery(Name = "sequence_max")] long? sequenceMax,
        [FromQuery(Name = "fragment_of")] Guid? fragmentOf,
        [FromQuery(Name = "has_friction_above")] double? hasFrictionAbove,
        [FromQuery(Name = "needs_review")] bool? needsReview,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        IEntryRepository entryRepo,
        CancellationToken ct)
    {
        var filters = new BrowseFilter
        {
            Query = query,
            MaxEntries = maxEntries,
            TopicPrefix = topicPrefix,
            ClaimsStatus = claimsStatus,
            Author = author,
            SequenceMin = sequenceMin,
            SequenceMax = sequenceMax,
            FragmentOf = fragmentOf,
            HasFrictionAbove = hasFrictionAbove,
            NeedsReview = needsReview,
            Limit = limit,
            Offset = offset,
        };

        if (filters.HasFilters)
        {
            // Filtered browse: direct SQL query
            var entries = await entryRepo.BrowseFilteredAsync(notebookId, filters, ct);
            return Results.Ok(new { entries, count = entries.Count });
        }
        else
        {
            // Unfiltered: return recent entries as a simple catalog
            // (Full entropy engine catalog integration is a future step)
            var defaultFilter = filters with { Limit = maxEntries ?? 50 };
            var entries = await entryRepo.BrowseFilteredAsync(notebookId, defaultFilter, ct);
            return Results.Ok(new { entries, count = entries.Count });
        }
    }
}
