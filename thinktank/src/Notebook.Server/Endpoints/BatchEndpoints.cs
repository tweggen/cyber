using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Types;
using Notebook.Data.Entities;
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
    /// External contributors (non-members of owning group) go through review queue.
    /// </summary>
    private static async Task<IResult> BatchWrite(
        Guid notebookId,
        [FromBody] BatchWriteRequest request,
        IAccessControl acl,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        INotebookRepository notebookRepo,
        IOrganizationRepository orgRepo,
        IReviewRepository reviewRepo,
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

        // Determine if the submitter is an external contributor (non-member of owning group)
        var requiresReview = await IsExternalContributorAsync(
            notebookId, authorId, notebookRepo, orgRepo, ct);

        var results = new List<BatchEntryResult>(request.Entries.Count);
        var jobsCreated = 0;

        await using var transaction = await entryRepo.BeginTransactionAsync(ct);

        foreach (var batchEntry in request.Entries)
        {
            // Validate classification assertion if provided
            if (batchEntry.ClassificationAssertion is not null)
            {
                var notebook = await notebookRepo.GetByIdAsync(notebookId, ct);
                if (notebook is not null)
                {
                    var assertionLevel = ClassificationLevel(batchEntry.ClassificationAssertion);
                    var notebookLevel = ClassificationLevel(notebook.Classification);
                    if (assertionLevel > notebookLevel)
                    {
                        return Results.BadRequest(new
                        {
                            error = $"Classification assertion ({batchEntry.ClassificationAssertion}) exceeds notebook classification ({notebook.Classification})"
                        });
                    }
                }
            }

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

                // Set review_status for external contributors
                if (requiresReview)
                    await SetPendingReviewAsync(reviewRepo, notebookId, artifactEntry.Id, authorId, ct);

                // Insert fragment entries
                foreach (var fragment in fragments)
                {
                    var fragmentEntry = await entryRepo.InsertEntryAsync(notebookId, authorId, new NewEntry
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

                    // Also mark fragments as pending if external
                    if (requiresReview)
                        await reviewRepo.SetEntryReviewStatusAsync(fragmentEntry.Id, "pending", ct);
                }

                // Only queue DISTILL_CLAIMS if not pending review
                if (!requiresReview)
                {
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
                }

                // Response returns the artifact entry ID
                results.Add(new BatchEntryResult
                {
                    EntryId = artifactEntry.Id,
                    CausalPosition = artifactEntry.Sequence,
                    IntegrationCost = artifactEntry.IntegrationCost?.CatalogShift ?? 0.0,
                    ClaimsStatus = ClaimsStatus.Pending,
                    ReviewStatus = requiresReview ? "pending" : "approved",
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

                if (requiresReview)
                {
                    // External contributor — enter review queue, no DISTILL_CLAIMS
                    await SetPendingReviewAsync(reviewRepo, notebookId, entry.Id, authorId, ct);
                }
                else
                {
                    // Member — queue DISTILL_CLAIMS immediately
                    var payload = JsonSerializer.SerializeToDocument(new
                    {
                        entry_id = entry.Id.ToString(),
                        content = filteredContent,
                        context_claims = (object?)null,
                        max_claims = 12,
                    });
                    await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
                    jobsCreated++;
                }

                results.Add(new BatchEntryResult
                {
                    EntryId = entry.Id,
                    CausalPosition = entry.Sequence,
                    IntegrationCost = entry.IntegrationCost?.CatalogShift ?? 0.0,
                    ClaimsStatus = ClaimsStatus.Pending,
                    ReviewStatus = requiresReview ? "pending" : "approved",
                });
            }
        }

        await transaction.CommitAsync(ct);

        AuditHelper.LogAction(audit, httpContext, "entry.batch_write", notebookId,
            targetType: "entries", targetId: null,
            detail: new { count = results.Count, jobs_created = jobsCreated,
                          requires_review = requiresReview });

        return Results.Created("", new BatchWriteResponse
        {
            Results = results,
            JobsCreated = jobsCreated,
        });
    }

    private static readonly string[] ClassificationOrder =
        ["PUBLIC", "INTERNAL", "CONFIDENTIAL", "SECRET", "TOP_SECRET"];

    private static int ClassificationLevel(string classification)
        => Array.IndexOf(ClassificationOrder, classification);

    /// <summary>
    /// Check if the submitter is an external contributor (not a member of the notebook's owning group).
    /// If the notebook has no owning group, all submitters are treated as members.
    /// </summary>
    private static async Task<bool> IsExternalContributorAsync(
        Guid notebookId, byte[] authorId,
        INotebookRepository notebookRepo, IOrganizationRepository orgRepo,
        CancellationToken ct)
    {
        var notebook = await notebookRepo.GetByIdAsync(notebookId, ct);
        if (notebook?.OwningGroupId is null)
            return false; // No owning group — no review required

        // Check if the author is the notebook owner (always treated as member)
        if (notebook.OwnerId.SequenceEqual(authorId))
            return false;

        var role = await orgRepo.GetGroupMembershipRoleAsync(notebook.OwningGroupId.Value, authorId, ct);
        return role is null; // null = not a member → external contributor
    }

    /// <summary>
    /// Set an entry to pending review status and create a review record.
    /// </summary>
    private static async Task SetPendingReviewAsync(
        IReviewRepository reviewRepo, Guid notebookId, Guid entryId, byte[] submitterId,
        CancellationToken ct)
    {
        await reviewRepo.SetEntryReviewStatusAsync(entryId, "pending", ct);
        await reviewRepo.CreateAsync(new EntryReviewEntity
        {
            Id = Guid.NewGuid(),
            NotebookId = notebookId,
            EntryId = entryId,
            Submitter = submitterId,
            Created = DateTimeOffset.UtcNow,
        }, ct);
    }
}
