using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Filters;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class ShareEndpoints
{
    public static void MapShareEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/notebooks/{notebookId}/share")
            .AddEndpointFilter<NotebookAccessFilter>()
            .RequireAuthorization("CanShare");

        group.MapPost("/", GrantAccess);
        group.MapDelete("/{authorHex}", RevokeAccess);
        group.MapGet("/", ListParticipants);
    }

    private static async Task<IResult> GrantAccess(
        Guid notebookId,
        [FromBody] ShareRequest request,
        IAccessRepository accessRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerHex))
            return Results.Unauthorized();
        var callerId = Convert.FromHexString(callerHex);

        // Only owner or admin-scoped users can share
        var isOwner = await accessRepo.IsOwnerAsync(notebookId, callerId, ct);
        var isAdmin = httpContext.User.HasClaim("scope", "notebook:admin");
        if (!isOwner && !isAdmin)
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.AuthorId) || request.AuthorId.Length != 64)
            return Results.BadRequest(new { error = "author_id must be a 64-character hex string" });

        byte[] targetAuthorId;
        try
        {
            targetAuthorId = Convert.FromHexString(request.AuthorId);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "author_id is not valid hex" });
        }

        await accessRepo.GrantAccessAsync(notebookId, targetAuthorId, request.Read, request.Write, ct);

        auditService.Log(callerId, "access.grant", $"notebook:{notebookId}",
            new { target = request.AuthorId, read = request.Read, write = request.Write },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new ShareResponse
        {
            NotebookId = notebookId,
            AuthorId = request.AuthorId.ToLowerInvariant(),
            Read = request.Read,
            Write = request.Write,
            Message = "Access granted",
        });
    }

    private static async Task<IResult> RevokeAccess(
        Guid notebookId,
        string authorHex,
        IAccessRepository accessRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerHex))
            return Results.Unauthorized();
        var callerId = Convert.FromHexString(callerHex);

        // Only owner or admin-scoped users can revoke
        var isOwner = await accessRepo.IsOwnerAsync(notebookId, callerId, ct);
        var isAdmin = httpContext.User.HasClaim("scope", "notebook:admin");
        if (!isOwner && !isAdmin)
            return Results.NotFound();

        byte[] targetAuthorId;
        try
        {
            targetAuthorId = Convert.FromHexString(authorHex);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "authorHex is not valid hex" });
        }

        await accessRepo.RevokeAccessAsync(notebookId, targetAuthorId, ct);

        auditService.Log(callerId, "access.revoke", $"notebook:{notebookId}",
            new { target = authorHex },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new RevokeShareResponse
        {
            NotebookId = notebookId,
            AuthorId = authorHex.ToLowerInvariant(),
            Message = "Access revoked",
        });
    }

    private static async Task<IResult> ListParticipants(
        Guid notebookId,
        IAccessRepository accessRepo,
        CancellationToken ct)
    {
        var accessList = await accessRepo.ListAccessAsync(notebookId, ct);

        var participants = accessList.Select(a => new ParticipantResponse
        {
            AuthorId = Convert.ToHexString(a.AuthorId).ToLowerInvariant(),
            Read = a.Read,
            Write = a.Write,
            Granted = a.Granted,
        }).ToList();

        return Results.Ok(new ListParticipantsResponse { Participants = participants });
    }
}
