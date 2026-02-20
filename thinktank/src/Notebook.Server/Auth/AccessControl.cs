using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Server.Services;

namespace Notebook.Server.Auth;

public class AccessControl(NotebookDbContext db, IAuditService audit, IHttpContextAccessor httpContextAccessor)
    : IAccessControl
{
    public async Task<IResult?> RequireReadAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (notebook.OwnerId.SequenceEqual(authorId))
            return null;

        var access = await db.NotebookAccess.AsNoTracking()
            .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == authorId && a.Read, ct);

        if (access is not null)
            return null;

        LogDenied(notebookId, authorId, "read");
        return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
    }

    public async Task<IResult?> RequireWriteAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (notebook.OwnerId.SequenceEqual(authorId))
            return null;

        var access = await db.NotebookAccess.AsNoTracking()
            .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == authorId && a.Write, ct);

        if (access is not null)
            return null;

        LogDenied(notebookId, authorId, "write");
        return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
    }

    public async Task<IResult?> RequireOwnerAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (notebook.OwnerId.SequenceEqual(authorId))
            return null;

        LogDenied(notebookId, authorId, "owner");
        return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
    }

    private void LogDenied(Guid notebookId, byte[] authorId, string requiredLevel)
    {
        var httpContext = httpContextAccessor.HttpContext;
        AuditHelper.LogAction(audit, httpContext, "access.denied", notebookId,
            targetType: "notebook", targetId: notebookId.ToString(),
            detail: new { required = requiredLevel });
    }
}
