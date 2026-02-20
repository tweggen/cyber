using Microsoft.EntityFrameworkCore;
using Notebook.Core.Security;
using Notebook.Data;
using Notebook.Data.Entities;
using Notebook.Server.Services;

namespace Notebook.Server.Auth;

public class AccessControl(
    NotebookDbContext db,
    IClearanceService clearance,
    IAuditService audit,
    IHttpContextAccessor httpContextAccessor)
    : IAccessControl
{
    public async Task<IResult?> RequireReadAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (!notebook.OwnerId.SequenceEqual(authorId))
        {
            var access = await db.NotebookAccess.AsNoTracking()
                .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == authorId && a.Read, ct);

            if (access is null)
            {
                LogDenied(notebookId, authorId, "read");
                return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
            }
        }

        return await CheckClearanceAsync(notebook, authorId, "read", ct);
    }

    public async Task<IResult?> RequireWriteAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (!notebook.OwnerId.SequenceEqual(authorId))
        {
            var access = await db.NotebookAccess.AsNoTracking()
                .FirstOrDefaultAsync(a => a.NotebookId == notebookId && a.AuthorId == authorId && a.Write, ct);

            if (access is null)
            {
                LogDenied(notebookId, authorId, "write");
                return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
            }
        }

        return await CheckClearanceAsync(notebook, authorId, "write", ct);
    }

    public async Task<IResult?> RequireOwnerAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        if (!notebook.OwnerId.SequenceEqual(authorId))
        {
            LogDenied(notebookId, authorId, "owner");
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
        }

        return await CheckClearanceAsync(notebook, authorId, "owner", ct);
    }

    private async Task<IResult?> CheckClearanceAsync(
        NotebookEntity notebook, byte[] authorId, string action, CancellationToken ct)
    {
        // Skip clearance check for notebooks not assigned to a group (legacy mode)
        if (notebook.OwningGroupId is null)
            return null;

        // Look up the organization for this group
        var group = await db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == notebook.OwningGroupId, ct);
        if (group is null)
            return null;

        var notebookLabel = new SecurityLabel(
            ClassificationLevelExtensions.ParseClassification(notebook.Classification),
            notebook.Compartments.ToHashSet());

        var principalClearance = await clearance.GetClearanceAsync(authorId, group.OrganizationId, ct);

        if (principalClearance.Dominates(notebookLabel))
            return null;

        LogDenied(notebook.Id, authorId, $"clearance:{action}");
        return Results.NotFound(new { error = $"Notebook {notebook.Id} not found" });
    }

    private void LogDenied(Guid notebookId, byte[] authorId, string requiredLevel)
    {
        var httpContext = httpContextAccessor.HttpContext;
        AuditHelper.LogAction(audit, httpContext, "access.denied", notebookId,
            targetType: "notebook", targetId: notebookId.ToString(),
            detail: new { required = requiredLevel });
    }
}
