using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class BatchEndpoints
{
    public static void MapBatchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/batch", BatchWrite)
            .RequireAuthorization("CanWrite");
    }

    /// <summary>
    /// Write up to 100 entries in a single transactional call.
    /// Each entry is normalized (HTML→markdown) and optionally fragmented if large.
    /// Each entry (or first fragment) is queued for claim distillation.
    /// </summary>
    private static async Task<IResult> BatchWrite(
        Guid notebookId,
        [FromBody] BatchWriteRequest request,
        IAccessControl acl,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        IContentNormalizer normalizer,
        IContentFilterPipeline filterPipeline,
        IMarkdownFragmenter fragmenter,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (request.Entries is not { Count: > 0 })
            return Results.BadRequest(new { error = "entries array is empty" });

        if (request.Entries.Count > 100)
            return Results.BadRequest(new { error = "batch size exceeds limit of 100 entries" });

        // Resolve author from JWT sub claim
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireWriteAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var results = new List<BatchEntryResult>(request.Entries.Count);
        var jobsCreated = 0;

        await using var transaction = await entryRepo.BeginTransactionAsync(ct);

        foreach (var batchEntry in request.Entries)
        {
            // 1. NORMALIZE: if content_type is text/html, convert to markdown
            var contentType = batchEntry.ContentType ?? "text/plain";
            var normalized = normalizer.Normalize(batchEntry.Content, contentType);

            // 2. FILTER: strip platform-specific boilerplate (Wikipedia, Confluence, etc.)
            var filtered = filterPipeline.Apply(normalized.Content, batchEntry.Source);
            var filteredContent = filtered.Content;
            var detectedSource = filtered.DetectedSource ?? batchEntry.Source;

            // 3. CHECK SIZE: fragment if content exceeds token budget (~4000 tokens ≈ 16000 chars)
            var fragments = normalized.ContentType == "text/markdown" || normalized.ContentType == "text/plain"
                ? fragmenter.Fragment(filteredContent)
                : [];

            if (fragments.Count > 0)
            {
                // Insert artifact entry (full content)
                var artifactEntry = await entryRepo.InsertEntryAsync(notebookId, authorId, new NewEntry
                {
                    Content = filteredContent,
                    ContentType = normalized.ContentType,
                    Topic = batchEntry.Topic,
                    References = batchEntry.References ?? [],
                    OriginalContentType = normalized.OriginalContentType,
                    Source = detectedSource,
                }, ct);

                // Insert fragment entries
                foreach (var fragment in fragments)
                {
                    await entryRepo.InsertEntryAsync(notebookId, authorId, new NewEntry
                    {
                        Content = fragment.Content,
                        ContentType = normalized.ContentType,
                        Topic = batchEntry.Topic,
                        References = [],
                        FragmentOf = artifactEntry.Id,
                        FragmentIndex = fragment.Index,
                        OriginalContentType = normalized.OriginalContentType,
                        Source = detectedSource,
                    }, ct);
                }

                // Queue DISTILL_CLAIMS for fragment 0 only (chaining handles the rest)
                var fragment0 = await entryRepo.GetFragmentAsync(notebookId, artifactEntry.Id, 0, ct);
                if (fragment0 is not null)
                {
                    var payload = JsonSerializer.SerializeToDocument(new
                    {
                        entry_id = fragment0.Id.ToString(),
                        content = fragments[0].Content,
                        context_claims = (object?)null,
                        max_claims = 12,
                    });
                    await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
                    jobsCreated++;
                }

                // Response returns the artifact entry ID
                results.Add(new BatchEntryResult
                {
                    EntryId = artifactEntry.Id,
                    CausalPosition = artifactEntry.Sequence,
                    IntegrationCost = artifactEntry.IntegrationCost?.CatalogShift ?? 0.0,
                    ClaimsStatus = ClaimsStatus.Pending,
                });
            }
            else
            {
                // Normal entry (no fragmentation needed)
                var entry = await entryRepo.InsertEntryAsync(notebookId, authorId, new NewEntry
                {
                    Content = filteredContent,
                    ContentType = normalized.ContentType,
                    Topic = batchEntry.Topic,
                    References = batchEntry.References ?? [],
                    FragmentOf = batchEntry.FragmentOf,
                    FragmentIndex = batchEntry.FragmentIndex,
                    OriginalContentType = normalized.OriginalContentType,
                    Source = detectedSource,
                }, ct);

                // Create DISTILL_CLAIMS job for this entry
                var payload = JsonSerializer.SerializeToDocument(new
                {
                    entry_id = entry.Id.ToString(),
                    content = filteredContent,
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
        }

        await transaction.CommitAsync(ct);

        AuditHelper.LogAction(audit, httpContext, "entry.batch_write", notebookId,
            targetType: "entries", targetId: null,
            detail: new { count = results.Count, jobs_created = jobsCreated });

        return Results.Created("", new BatchWriteResponse
        {
            Results = results,
            JobsCreated = jobsCreated,
        });
    }
}
