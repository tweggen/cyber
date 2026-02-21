using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;

namespace Notebook.Server.Endpoints;

public static class ClaimsEndpoints
{
    public static void MapClaimsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/entries/{entryId}/claims", UpdateClaims)
            .RequireAuthorization("CanWrite");
    }

    /// <summary>
    /// Write claims to an entry (called by robot workers after distillation).
    /// Immutable: rejects if claims are already written.
    /// </summary>
    private static async Task<IResult> UpdateClaims(
        Guid notebookId,
        Guid entryId,
        [FromBody] UpdateClaimsRequest request,
        IAccessControl acl,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        if (request.Claims is not { Count: > 0 })
            return Results.BadRequest(new { error = "claims array is empty" });

        if (request.Claims.Count > 20)
            return Results.BadRequest(new { error = "claims array exceeds maximum of 20" });

        // Atomic update: only succeeds if claims_status is still 'pending'
        var updated = await entryRepo.UpdateEntryClaimsAsync(
            entryId, notebookId, request.Claims, ct);

        if (!updated)
            return Results.Conflict(new { error = "claims already set or entry not found" });

        // Create EMBED_CLAIMS job for semantic nearest-neighbor comparison
        var embedJobsCreated = 0;
        if (request.Claims.Count > 0)
        {
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entryId.ToString(),
                claim_texts = request.Claims.Select(c => c.Text).ToList(),
            });

            await jobRepo.InsertJobAsync(notebookId, "EMBED_CLAIMS", payload, ct);
            embedJobsCreated = 1;
        }

        return Results.Ok(new UpdateClaimsResponse
        {
            EntryId = entryId,
            ClaimsStatus = ClaimsStatus.Distilled,
            ComparisonJobsCreated = embedJobsCreated,
        });
    }
}
