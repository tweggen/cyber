using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Core.Security;
using Notebook.Data;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class NotebookEndpoints
{
    public static void MapNotebookEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks", ListNotebooks).RequireAuthorization("CanRead");
        routes.MapPost("/notebooks", CreateNotebook).RequireAuthorization("CanWrite");
        routes.MapDelete("/notebooks/{notebookId}", DeleteNotebook).RequireAuthorization("CanAdmin");
        routes.MapPatch("/notebooks/{notebookId}", RenameNotebook).RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> ListNotebooks(
        INotebookRepository notebookRepo,
        NotebookDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var notebooks = await notebookRepo.ListNotebooksAsync(authorId, ct);

        var summaries = new List<NotebookSummaryResponse>(notebooks.Count);
        foreach (var n in notebooks)
        {
            var ownerHex = Convert.ToHexString(n.OwnerId).ToLowerInvariant();
            var isOwner = n.OwnerId.SequenceEqual(authorId);
            var totalEntries = await notebookRepo.CountEntriesAsync(n.Id, ct);
            var participantCount = await notebookRepo.CountParticipantsAsync(n.Id, ct);

            // Look up actual permissions from notebook_access
            var access = await db.NotebookAccess.AsNoTracking()
                .FirstOrDefaultAsync(a => a.NotebookId == n.Id && a.AuthorId == authorId, ct);

            summaries.Add(new NotebookSummaryResponse
            {
                Id = n.Id,
                Name = n.Name,
                Owner = ownerHex,
                IsOwner = isOwner,
                Permissions = new NotebookPermissionsResponse
                {
                    Read = access?.Read ?? isOwner,
                    Write = access?.Write ?? isOwner,
                },
                TotalEntries = totalEntries,
                TotalEntropy = 0.0,
                LastActivitySequence = n.CurrentSequence,
                ParticipantCount = participantCount,
                Classification = n.Classification,
                Compartments = n.Compartments,
            });
        }

        return Results.Ok(new ListNotebooksResponse { Notebooks = summaries });
    }

    private static async Task<IResult> CreateNotebook(
        [FromBody] CreateNotebookRequest request,
        INotebookRepository notebookRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        // Parse requested classification (default: INTERNAL)
        var classification = ClassificationLevel.Internal;
        if (!string.IsNullOrWhiteSpace(request.Classification))
            classification = ClassificationLevelExtensions.ParseClassification(request.Classification);
        var compartments = request.Compartments ?? [];

        var notebook = await notebookRepo.CreateNotebookAsync(
            request.Name.Trim(), authorId, ct,
            classification.ToDbString(), compartments);

        AuditHelper.LogAction(audit, httpContext, "notebook.create", notebook.Id,
            targetType: "notebook", targetId: notebook.Id.ToString(),
            detail: new { classification = notebook.Classification, compartments = notebook.Compartments });

        return Results.Created($"/notebooks/{notebook.Id}", new CreateNotebookResponse
        {
            Id = notebook.Id,
            Name = notebook.Name,
            Owner = Convert.ToHexString(notebook.OwnerId).ToLowerInvariant(),
            Created = notebook.Created,
            Classification = notebook.Classification,
            Compartments = notebook.Compartments,
        });
    }

    private static async Task<IResult> DeleteNotebook(
        Guid notebookId,
        IAccessControl acl,
        INotebookRepository notebookRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireOwnerAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var deleted = await notebookRepo.DeleteNotebookAsync(notebookId, authorId, ct);
        if (!deleted)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found or not owned by you" });

        AuditHelper.LogAction(audit, httpContext, "notebook.delete", notebookId,
            targetType: "notebook", targetId: notebookId.ToString());

        return Results.Ok(new DeleteNotebookResponse
        {
            Id = notebookId,
            Message = "Notebook deleted",
        });
    }

    private static async Task<IResult> RenameNotebook(
        Guid notebookId,
        [FromBody] RenameNotebookRequest request,
        IAccessControl acl,
        INotebookRepository notebookRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireOwnerAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var notebook = await notebookRepo.RenameNotebookAsync(notebookId, request.Name.Trim(), authorId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found or not owned by you" });

        AuditHelper.LogAction(audit, httpContext, "notebook.rename", notebookId,
            targetType: "notebook", targetId: notebookId.ToString(),
            detail: new { name = notebook.Name });

        return Results.Ok(new RenameNotebookResponse
        {
            Id = notebook.Id,
            Name = notebook.Name,
        });
    }
}
