using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;

namespace Notebook.Server.Endpoints;

public static class NotebookEndpoints
{
    public static void MapNotebookEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks", ListNotebooks).RequireAuthorization();
        routes.MapPost("/notebooks", CreateNotebook).RequireAuthorization();
        routes.MapDelete("/notebooks/{notebookId}", DeleteNotebook).RequireAuthorization();
        routes.MapPatch("/notebooks/{notebookId}", RenameNotebook).RequireAuthorization();
    }

    private static async Task<IResult> ListNotebooks(
        INotebookRepository notebookRepo,
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

            summaries.Add(new NotebookSummaryResponse
            {
                Id = n.Id,
                Name = n.Name,
                Owner = ownerHex,
                IsOwner = isOwner,
                Permissions = new NotebookPermissionsResponse { Read = true, Write = isOwner },
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

        return Results.Ok(new RenameNotebookResponse
        {
            Id = notebook.Id,
            Name = notebook.Name,
        });
    }
}
