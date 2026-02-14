using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;
using Notebook.Server.Models;

namespace Notebook.Server.Endpoints;

public static class ClaimsEndpoints
{
    public static void MapClaimsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/entries/{entryId}/claims", UpdateClaims)
            .RequireAuthorization();
    }

    /// <summary>
    /// Write claims to an entry (called by robot workers after distillation).
    /// Immutable: rejects if claims are already written.
    /// </summary>
    private static async Task<IResult> UpdateClaims(
        Guid notebookId,
        Guid entryId,
        [FromBody] UpdateClaimsRequest request,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        CancellationToken ct)
    {
        if (request.Claims is not { Count: > 0 })
            return Results.BadRequest(new { error = "claims array is empty" });

        if (request.Claims.Count > 20)
            return Results.BadRequest(new { error = "claims array exceeds maximum of 20" });

        // Atomic update: only succeeds if claims_status is still 'pending'
        var updated = await entryRepo.UpdateEntryClaimsAsync(
            entryId, notebookId, request.Claims, ct);

        if (!updated)
            return Results.Conflict(new { error = "claims already set or entry not found" });

        // Create COMPARE_CLAIMS jobs against relevant topic indices
        var topicIndices = await entryRepo.FindTopicIndicesAsync(notebookId, ct);
        var comparisonJobsCreated = 0;

        foreach (var (indexId, indexClaims) in topicIndices)
        {
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entryId.ToString(),
                compare_against_id = indexId.ToString(),
                claims_a = indexClaims,
                claims_b = request.Claims,
            });

            await jobRepo.InsertJobAsync(notebookId, "COMPARE_CLAIMS", payload, ct);
            comparisonJobsCreated++;
        }

        return Results.Ok(new UpdateClaimsResponse
        {
            EntryId = entryId,
            ClaimsStatus = ClaimsStatus.Distilled,
            ComparisonJobsCreated = comparisonJobsCreated,
        });
    }
}
