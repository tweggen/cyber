using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;
using Notebook.Server.Filters;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/batch", BatchWrite)
            .AddEndpointFilter<NotebookAccessFilter>()
            .RequireAuthorization("CanWrite");
    }

    /// <summary>
    /// Write up to 100 entries in a single transactional call.
    /// Each entry is automatically queued for claim distillation.
    /// </summary>
    private static async Task<IResult> BatchWrite(
        Guid notebookId,
        [FromBody] BatchWriteRequest request,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (request.Entries is not { Count: > 0 })
            return Results.BadRequest(new { error = "entries array is empty" });

        if (request.Entries.Count > 100)
            return Results.BadRequest(new { error = "batch size exceeds limit of 100 entries" });

        if (!await entryRepo.NotebookExistsAsync(notebookId, ct))
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        // Resolve author from JWT sub claim
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var results = new List<BatchEntryResult>(request.Entries.Count);
        var jobsCreated = 0;

        await using var transaction = await entryRepo.BeginTransactionAsync(ct);

        foreach (var batchEntry in request.Entries)
        {
            var entry = await entryRepo.InsertEntryAsync(notebookId, authorId, new NewEntry
            {
                Content = batchEntry.Content,
                ContentType = batchEntry.ContentType ?? "text/plain",
                Topic = batchEntry.Topic,
                References = batchEntry.References ?? [],
                FragmentOf = batchEntry.FragmentOf,
                FragmentIndex = batchEntry.FragmentIndex,
            }, ct);

            // Create DISTILL_CLAIMS job for this entry
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entry.Id.ToString(),
                content = batchEntry.Content,
                context_claims = (object?)null,
                max_claims = 12,
            });

            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
            jobsCreated++;

            results.Add(new BatchEntryResult
            {
                EntryId = entry.Id,
                CausalPosition = entry.Sequence,
                IntegrationCost = entry.IntegrationCost?.CatalogShift ?? 0.0,
                ClaimsStatus = ClaimsStatus.Pending,
            });
        }

        await transaction.CommitAsync(ct);

        auditService.Log(authorId, "entry.write", $"notebook:{notebookId}",
            new { count = results.Count, jobs_created = jobsCreated },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Created("", new BatchWriteResponse
        {
            Results = results,
            JobsCreated = jobsCreated,
        });
    }
}
