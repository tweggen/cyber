using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/reviews", ListReviews)
            .RequireAuthorization("CanAdmin");
        routes.MapPost("/notebooks/{notebookId}/reviews/{reviewId}/approve", ApproveReview)
            .RequireAuthorization("CanAdmin");
        routes.MapPost("/notebooks/{notebookId}/reviews/{reviewId}/reject", RejectReview)
            .RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> ListReviews(
        Guid notebookId,
        [FromQuery] string? status,
        IReviewRepository reviewRepo,
        IAccessControl acl,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireAdminAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var reviews = await reviewRepo.ListByNotebookAsync(notebookId, status, ct);
        var pendingCount = await reviewRepo.CountPendingAsync(notebookId, ct);

        return Results.Ok(new ListReviewsResponse
        {
            Reviews = reviews.Select(MapToResponse).ToList(),
            PendingCount = pendingCount,
        });
    }

    private static async Task<IResult> ApproveReview(
        Guid notebookId,
        Guid reviewId,
        IReviewRepository reviewRepo,
        IEntryRepository entryRepo,
        IJobRepository jobRepo,
        IAccessControl acl,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireAdminAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var review = await reviewRepo.GetAsync(reviewId, ct);
        if (review is null || review.NotebookId != notebookId)
            return Results.NotFound(new { error = $"Review {reviewId} not found" });

        if (review.Status != "pending")
            return Results.BadRequest(new { error = $"Review is already {review.Status}" });

        // Approve the review and update the entry
        await reviewRepo.ApproveAsync(reviewId, authorId, ct);
        await reviewRepo.SetEntryReviewStatusAsync(review.EntryId, "approved", ct);

        // Queue DISTILL_CLAIMS job — the entry now enters the entropy pipeline
        var entry = await entryRepo.GetEntryAsync(review.EntryId, notebookId, ct);
        if (entry is not null)
        {
            var content = Encoding.UTF8.GetString(entry.Content);
            var payload = JsonSerializer.SerializeToDocument(new
            {
                entry_id = entry.Id.ToString(),
                content,
                context_claims = (object?)null,
                max_claims = 12,
            });
            await jobRepo.InsertJobAsync(notebookId, "DISTILL_CLAIMS", payload, ct);
        }

        await AuditHelper.LogActionAsync(audit, httpContext, "entry.review.approve", notebookId,
            targetType: "entry_review", targetId: reviewId.ToString(),
            detail: new { entry_id = review.EntryId });

        return Results.Ok(new { review_id = reviewId, status = "approved" });
    }

    private static async Task<IResult> RejectReview(
        Guid notebookId,
        Guid reviewId,
        IReviewRepository reviewRepo,
        IAccessControl acl,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireAdminAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var review = await reviewRepo.GetAsync(reviewId, ct);
        if (review is null || review.NotebookId != notebookId)
            return Results.NotFound(new { error = $"Review {reviewId} not found" });

        if (review.Status != "pending")
            return Results.BadRequest(new { error = $"Review is already {review.Status}" });

        // Reject — no reason given (by design, to prevent information flow)
        await reviewRepo.RejectAsync(reviewId, authorId, ct);
        await reviewRepo.SetEntryReviewStatusAsync(review.EntryId, "rejected", ct);

        await AuditHelper.LogActionAsync(audit, httpContext, "entry.review.reject", notebookId,
            targetType: "entry_review", targetId: reviewId.ToString(),
            detail: new { entry_id = review.EntryId });

        // Return only status — no reason field
        return Results.Ok(new { review_id = reviewId, status = "rejected" });
    }

    private static ReviewResponse MapToResponse(EntryReviewEntity review)
    {
        return new ReviewResponse
        {
            Id = review.Id,
            NotebookId = review.NotebookId,
            EntryId = review.EntryId,
            Submitter = Convert.ToHexString(review.Submitter).ToLowerInvariant(),
            Status = review.Status,
            Reviewer = review.Reviewer is not null
                ? Convert.ToHexString(review.Reviewer).ToLowerInvariant()
                : null,
            ReviewedAt = review.ReviewedAt,
            Created = review.Created,
        };
    }
}
