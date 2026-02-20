using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Filters;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class NotebookEndpoints
{
    public static void MapNotebookEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks", ListNotebooks).RequireAuthorization("CanRead");
        routes.MapPost("/notebooks", CreateNotebook).RequireAuthorization("CanWrite");
        routes.MapDelete("/notebooks/{notebookId}", DeleteNotebook)
            .AddEndpointFilter<NotebookAccessFilter>()
            .RequireAuthorization("CanWrite");
        routes.MapPatch("/notebooks/{notebookId}", RenameNotebook)
            .AddEndpointFilter<NotebookAccessFilter>()
            .RequireAuthorization("CanWrite");
    }

    private static async Task<IResult> ListNotebooks(
        INotebookRepository notebookRepo,
        IAccessRepository accessRepo,
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

            // Get actual permissions from ACL
            bool canRead, canWrite;
            if (isOwner)
            {
                canRead = true;
                canWrite = true;
            }
            else
            {
                var access = await accessRepo.GetAccessAsync(n.Id, authorId, ct);
                canRead = access?.Read ?? false;
                canWrite = access?.Write ?? false;
            }

            summaries.Add(new NotebookSummaryResponse
            {
                Id = n.Id,
                Name = n.Name,
                Owner = ownerHex,
                IsOwner = isOwner,
                Permissions = new NotebookPermissionsResponse { Read = canRead, Write = canWrite },
                TotalEntries = totalEntries,
                TotalEntropy = 0.0,
                LastActivitySequence = n.CurrentSequence,
                ParticipantCount = participantCount,
            });
        }

        return Results.Ok(new ListNotebooksResponse { Notebooks = summaries });
    }

    private static async Task<IResult> CreateNotebook(
        [FromBody] CreateNotebookRequest request,
        INotebookRepository notebookRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var notebook = await notebookRepo.CreateNotebookAsync(request.Name.Trim(), authorId, ct);

        auditService.Log(authorId, "notebook.create", $"notebook:{notebook.Id}",
            new { name = notebook.Name },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Created($"/notebooks/{notebook.Id}", new CreateNotebookResponse
        {
            Id = notebook.Id,
            Name = notebook.Name,
            Owner = Convert.ToHexString(notebook.OwnerId).ToLowerInvariant(),
            Created = notebook.Created,
        });
    }

    private static async Task<IResult> DeleteNotebook(
        Guid notebookId,
        INotebookRepository notebookRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deleted = await notebookRepo.DeleteNotebookAsync(notebookId, authorId, ct);
        if (!deleted)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found or not owned by you" });

        auditService.Log(authorId, "notebook.delete", $"notebook:{notebookId}",
            detail: null,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new DeleteNotebookResponse
        {
            Id = notebookId,
            Message = "Notebook deleted",
        });
    }

    private static async Task<IResult> RenameNotebook(
        Guid notebookId,
        [FromBody] RenameNotebookRequest request,
        INotebookRepository notebookRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var notebook = await notebookRepo.RenameNotebookAsync(notebookId, request.Name.Trim(), authorId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found or not owned by you" });

        auditService.Log(authorId, "notebook.rename", $"notebook:{notebookId}",
            new { name = notebook.Name },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new RenameNotebookResponse
        {
            Id = notebook.Id,
            Name = notebook.Name,
        });
    }
}
