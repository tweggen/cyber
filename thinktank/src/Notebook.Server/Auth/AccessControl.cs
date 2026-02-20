using Microsoft.EntityFrameworkCore;
using Notebook.Core.Security;
using Notebook.Data;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;
using Notebook.Server.Services;

namespace Notebook.Server.Auth;

public class AccessControl(
    NotebookDbContext db,
    IOrganizationRepository orgRepo,
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

        var tier = await GetEffectiveTierForNotebookAsync(notebook, authorId, ct);
        if (tier < AccessTier.Read)
        {
            LogDenied(notebookId, authorId, "read");
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
        }

        return await CheckClearanceAsync(notebook, authorId, "read", ct);
    }

    public async Task<IResult?> RequireWriteAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        var tier = await GetEffectiveTierForNotebookAsync(notebook, authorId, ct);
        if (tier < AccessTier.ReadWrite)
        {
            LogDenied(notebookId, authorId, "write");
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
        }

        return await CheckClearanceAsync(notebook, authorId, "write", ct);
    }

    public async Task<IResult?> RequireAdminAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });

        var tier = await GetEffectiveTierForNotebookAsync(notebook, authorId, ct);
        if (tier < AccessTier.Admin)
        {
            LogDenied(notebookId, authorId, "admin");
            return Results.NotFound(new { error = $"Notebook {notebookId} not found" });
        }

        return await CheckClearanceAsync(notebook, authorId, "admin", ct);
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

    public async Task<AccessTier> GetEffectiveTierAsync(Guid notebookId, byte[] authorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notebookId, ct);
        if (notebook is null)
            return AccessTier.Existence;

        return await GetEffectiveTierForNotebookAsync(notebook, authorId, ct);
    }

    private async Task<AccessTier> GetEffectiveTierForNotebookAsync(
        NotebookEntity notebook, byte[] authorId, CancellationToken ct)
    {
        // Owner always has admin tier
        if (notebook.OwnerId.SequenceEqual(authorId))
            return AccessTier.Admin;

        // Check direct ACL
        var access = await db.NotebookAccess.AsNoTracking()
            .FirstOrDefaultAsync(a => a.NotebookId == notebook.Id && a.AuthorId == authorId, ct);
        var directTier = access is not null
            ? AccessTierExtensions.ParseAccessTier(access.Tier)
            : AccessTier.Existence;

        // Check group-propagated tier
        var groupTier = AccessTier.Existence;
        if (notebook.OwningGroupId is not null)
        {
            var role = await orgRepo.GetGroupMembershipRoleAsync(notebook.OwningGroupId.Value, authorId, ct);
            if (role is not null)
            {
                groupTier = role == "admin" ? AccessTier.Admin : AccessTier.ReadWrite;
            }
        }

        // Return the highest tier found
        return directTier >= groupTier ? directTier : groupTier;
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
