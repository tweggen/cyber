using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Entities;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class ShareEndpoints
{
    public static void MapShareEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/notebooks/{notebookId}/share", ShareNotebook)
            .RequireAuthorization("CanShare");
        routes.MapDelete("/notebooks/{notebookId}/share/{authorIdHex}", RevokeShare)
            .RequireAuthorization("CanShare");
    }

    private static async Task<IResult> ShareNotebook(
        Guid notebookId,
        [FromBody] ShareRequest request,
        IAccessControl acl,
        NotebookDbContext db,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerHex))
            return Results.Unauthorized();
        var callerId = Convert.FromHexString(callerHex);

        var deny = await acl.RequireOwnerAsync(notebookId, callerId, ct);
        if (deny is not null) return deny;

        var targetAuthorId = Convert.FromHexString(request.AuthorId);

        // Ensure the author exists (auto-create synthetic author row)
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [targetAuthorId, targetAuthorId], ct);

        var existing = await db.NotebookAccess
            .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == targetAuthorId, ct);

        if (existing is not null)
        {
            existing.Read = request.Permissions.Read;
            existing.Write = request.Permissions.Write;
            existing.Granted = DateTimeOffset.UtcNow;
        }
        else
        {
            db.NotebookAccess.Add(new NotebookAccessEntity
            {
                NotebookId = notebookId,
                AuthorId = targetAuthorId,
                Read = request.Permissions.Read,
                Write = request.Permissions.Write,
                Granted = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);

        AuditHelper.LogAction(audit, httpContext, "notebook.share", notebookId,
            targetType: "author", targetId: request.AuthorId,
            detail: new { read = request.Permissions.Read, write = request.Permissions.Write });

        return Results.Ok(new ShareResponse
        {
            NotebookId = notebookId,
            AuthorId = request.AuthorId,
            Permissions = request.Permissions,
        });
    }

    private static async Task<IResult> RevokeShare(
        Guid notebookId,
        string authorIdHex,
        IAccessControl acl,
        NotebookDbContext db,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(callerHex))
            return Results.Unauthorized();
        var callerId = Convert.FromHexString(callerHex);

        var deny = await acl.RequireOwnerAsync(notebookId, callerId, ct);
        if (deny is not null) return deny;

        var targetAuthorId = Convert.FromHexString(authorIdHex);

        var access = await db.NotebookAccess
            .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == targetAuthorId, ct);

        if (access is null)
            return Results.NotFound(new { error = $"No access record found for author {authorIdHex}" });

        db.NotebookAccess.Remove(access);
        await db.SaveChangesAsync(ct);

        AuditHelper.LogAction(audit, httpContext, "notebook.revoke", notebookId,
            targetType: "author", targetId: authorIdHex);

        return Results.Ok(new RevokeResponse
        {
            NotebookId = notebookId,
            AuthorId = authorIdHex,
            Message = "Access revoked",
        });
    }
}
