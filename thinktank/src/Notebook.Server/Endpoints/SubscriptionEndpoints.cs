using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class SubscriptionEndpoints
{
    private static readonly string[] ClassificationOrder =
        ["PUBLIC", "INTERNAL", "CONFIDENTIAL", "SECRET", "TOP_SECRET"];

    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/subscriptions", CreateSubscription)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/notebooks/{notebookId}/subscriptions", ListSubscriptions)
            .RequireAuthorization("CanRead");
        routes.MapGet("/notebooks/{notebookId}/subscriptions/{subId}", GetSubscription)
            .RequireAuthorization("CanRead");
        routes.MapPost("/notebooks/{notebookId}/subscriptions/{subId}/sync", TriggerSync)
            .RequireAuthorization("CanAdmin");
        routes.MapDelete("/notebooks/{notebookId}/subscriptions/{subId}", DeleteSubscription)
            .RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> CreateSubscription(
        Guid notebookId,
        [FromBody] CreateSubscriptionRequest request,
        ISubscriptionRepository subRepo,
        INotebookRepository notebookRepo,
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

        // Validate scope
        if (request.Scope is not ("catalog" or "claims" or "entries"))
            return Results.BadRequest(new { error = "scope must be 'catalog', 'claims', or 'entries'" });

        // Validate discount factor
        if (request.DiscountFactor is <= 0 or > 1.0)
            return Results.BadRequest(new { error = "discount_factor must be > 0 and <= 1.0" });

        // Validate poll interval
        if (request.PollIntervalSeconds < 10)
            return Results.BadRequest(new { error = "poll_interval_s must be >= 10" });

        // No self-subscription
        if (notebookId == request.SourceId)
            return Results.BadRequest(new { error = "Cannot subscribe to self" });

        // Load both notebooks for classification/compartment validation
        var subscriber = await notebookRepo.GetByIdAsync(notebookId, ct);
        if (subscriber is null)
            return Results.NotFound(new { error = $"Subscriber notebook {notebookId} not found" });

        var source = await notebookRepo.GetByIdAsync(request.SourceId, ct);
        if (source is null)
            return Results.NotFound(new { error = $"Source notebook {request.SourceId} not found" });

        // Classification check: subscriber >= source
        var subscriberLevel = Array.IndexOf(ClassificationOrder, subscriber.Classification);
        var sourceLevel = Array.IndexOf(ClassificationOrder, source.Classification);
        if (subscriberLevel < sourceLevel)
            return Results.BadRequest(new { error = $"Subscriber classification ({subscriber.Classification}) must be >= source classification ({source.Classification})" });

        // Compartment check: subscriber compartments ⊇ source compartments
        var subscriberCompartments = new HashSet<string>(subscriber.Compartments);
        if (!source.Compartments.All(c => subscriberCompartments.Contains(c)))
            return Results.BadRequest(new { error = "Subscriber compartments must be a superset of source compartments" });

        // No duplicates
        if (await subRepo.ExistsAsync(notebookId, request.SourceId, ct))
            return Results.Conflict(new { error = "Subscription already exists" });

        // No cycles
        if (await subRepo.WouldCreateCycleAsync(notebookId, request.SourceId, ct))
            return Results.BadRequest(new { error = "Subscription would create a cycle" });

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            SubscriberId = notebookId,
            SourceId = request.SourceId,
            Scope = request.Scope,
            TopicFilter = request.TopicFilter,
            ApprovedBy = authorId,
            DiscountFactor = request.DiscountFactor,
            PollIntervalSeconds = request.PollIntervalSeconds,
            Created = DateTimeOffset.UtcNow,
        };

        await subRepo.CreateAsync(subscription, ct);

        AuditHelper.LogAction(audit, httpContext, "subscription.create", notebookId,
            targetType: "subscription", targetId: subscription.Id.ToString(),
            detail: new { source_id = request.SourceId, scope = request.Scope });

        var response = MapToResponse(subscription);
        return Results.Created($"/notebooks/{notebookId}/subscriptions/{subscription.Id}", response);
    }

    private static async Task<IResult> ListSubscriptions(
        Guid notebookId,
        ISubscriptionRepository subRepo,
        IAccessControl acl,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireReadAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var subs = await subRepo.ListBySubscriberAsync(notebookId, ct);

        return Results.Ok(new ListSubscriptionsResponse
        {
            Subscriptions = subs.Select(MapToResponse).ToList(),
        });
    }

    private static async Task<IResult> GetSubscription(
        Guid notebookId,
        Guid subId,
        ISubscriptionRepository subRepo,
        IAccessControl acl,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireReadAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var sub = await subRepo.GetAsync(subId, ct);
        if (sub is null || sub.SubscriberId != notebookId)
            return Results.NotFound(new { error = $"Subscription {subId} not found" });

        return Results.Ok(MapToResponse(sub));
    }

    private static async Task<IResult> TriggerSync(
        Guid notebookId,
        Guid subId,
        ISubscriptionRepository subRepo,
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

        var sub = await subRepo.GetAsync(subId, ct);
        if (sub is null || sub.SubscriberId != notebookId)
            return Results.NotFound(new { error = $"Subscription {subId} not found" });

        // Set last_sync_at to null so the sync loop picks it up immediately
        await subRepo.SetSyncStatusAsync(subId, "idle", null, ct);

        AuditHelper.LogAction(audit, httpContext, "subscription.sync_triggered", notebookId,
            targetType: "subscription", targetId: subId.ToString());

        return Results.Ok(new { message = "Sync triggered", subscription_id = subId });
    }

    private static async Task<IResult> DeleteSubscription(
        Guid notebookId,
        Guid subId,
        ISubscriptionRepository subRepo,
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

        var sub = await subRepo.GetAsync(subId, ct);
        if (sub is null || sub.SubscriberId != notebookId)
            return Results.NotFound(new { error = $"Subscription {subId} not found" });

        await subRepo.DeleteAsync(subId, ct);

        AuditHelper.LogAction(audit, httpContext, "subscription.delete", notebookId,
            targetType: "subscription", targetId: subId.ToString());

        return Results.Ok(new { id = subId, message = "Subscription deleted" });
    }

    private static SubscriptionResponse MapToResponse(SubscriptionEntity sub)
    {
        return new SubscriptionResponse
        {
            Id = sub.Id,
            SubscriberId = sub.SubscriberId,
            SourceId = sub.SourceId,
            Scope = sub.Scope,
            TopicFilter = sub.TopicFilter,
            SyncStatus = sub.SyncStatus,
            SyncWatermark = sub.SyncWatermark,
            LastSyncAt = sub.LastSyncAt,
            SyncError = sub.SyncError,
            MirroredCount = sub.MirroredCount,
            DiscountFactor = sub.DiscountFactor,
            PollIntervalSeconds = sub.PollIntervalSeconds,
            Created = sub.Created,
        };
    }
}
