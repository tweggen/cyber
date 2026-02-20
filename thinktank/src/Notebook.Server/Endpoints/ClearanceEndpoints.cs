using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Core.Security;
using Notebook.Data;
using Notebook.Data.Entities;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class ClearanceEndpoints
{
    public static void MapClearanceEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/clearances", GrantClearance).RequireAuthorization("CanAdmin");
        routes.MapDelete("/clearances/{authorIdHex}/{organizationId}", RevokeClearance)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/organizations/{organizationId}/clearances", ListClearances)
            .RequireAuthorization("CanRead");
        routes.MapPost("/admin/cache/flush", FlushCache).RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> GrantClearance(
        [FromBody] GrantClearanceRequest request,
        NotebookDbContext db,
        IClearanceService clearanceService,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AuthorId))
            return Results.BadRequest(new { error = "author_id is required" });

        // Validate classification level
        var level = ClassificationLevelExtensions.ParseClassification(request.MaxLevel);
        var dbLevel = level.ToDbString();

        byte[] authorId;
        try
        {
            authorId = Convert.FromHexString(request.AuthorId);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Invalid author_id hex" });
        }

        // Verify organization exists
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!orgExists)
            return Results.NotFound(new { error = $"Organization {request.OrganizationId} not found" });

        // Ensure author exists
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [authorId, authorId], ct);

        // Upsert clearance
        var existing = await db.PrincipalClearances
            .FirstOrDefaultAsync(c => c.AuthorId == authorId && c.OrganizationId == request.OrganizationId, ct);

        var grantingAuthorHex = httpContext.User.FindFirst("sub")?.Value;
        byte[]? grantedBy = null;
        if (!string.IsNullOrEmpty(grantingAuthorHex))
            grantedBy = Convert.FromHexString(grantingAuthorHex);

        if (existing is not null)
        {
            existing.MaxLevel = dbLevel;
            existing.Compartments = request.Compartments;
            existing.Granted = DateTimeOffset.UtcNow;
            existing.GrantedBy = grantedBy;
        }
        else
        {
            db.PrincipalClearances.Add(new PrincipalClearanceEntity
            {
                AuthorId = authorId,
                OrganizationId = request.OrganizationId,
                MaxLevel = dbLevel,
                Compartments = request.Compartments,
                Granted = DateTimeOffset.UtcNow,
                GrantedBy = grantedBy,
            });
        }

        await db.SaveChangesAsync(ct);

        // Evict cache for this principal+org pair
        clearanceService.EvictCache(authorId, request.OrganizationId);

        AuditHelper.LogAction(audit, httpContext, "clearance.grant", null,
            targetType: "clearance",
            targetId: $"{request.AuthorId}:{request.OrganizationId}",
            detail: new { max_level = dbLevel, compartments = request.Compartments });

        var entity = existing ?? await db.PrincipalClearances.AsNoTracking()
            .FirstAsync(c => c.AuthorId == authorId && c.OrganizationId == request.OrganizationId, ct);

        return Results.Ok(new GrantClearanceResponse
        {
            AuthorId = Convert.ToHexString(entity.AuthorId).ToLowerInvariant(),
            OrganizationId = entity.OrganizationId,
            MaxLevel = entity.MaxLevel,
            Compartments = entity.Compartments,
            Granted = entity.Granted,
        });
    }

    private static async Task<IResult> RevokeClearance(
        string authorIdHex,
        Guid organizationId,
        NotebookDbContext db,
        IClearanceService clearanceService,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        byte[] authorId;
        try
        {
            authorId = Convert.FromHexString(authorIdHex);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Invalid author_id hex" });
        }

        var entity = await db.PrincipalClearances
            .FirstOrDefaultAsync(c => c.AuthorId == authorId && c.OrganizationId == organizationId, ct);

        if (entity is null)
            return Results.NotFound(new { error = "Clearance not found" });

        db.PrincipalClearances.Remove(entity);
        await db.SaveChangesAsync(ct);

        // Evict cache
        clearanceService.EvictCache(authorId, organizationId);

        AuditHelper.LogAction(audit, httpContext, "clearance.revoke", null,
            targetType: "clearance",
            targetId: $"{authorIdHex}:{organizationId}");

        return Results.Ok(new RevokeClearanceResponse { Message = "Clearance revoked" });
    }

    private static async Task<IResult> ListClearances(
        Guid organizationId,
        NotebookDbContext db,
        CancellationToken ct)
    {
        var clearances = await db.PrincipalClearances.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.Granted)
            .ToListAsync(ct);

        return Results.Ok(new ListClearancesResponse
        {
            Clearances = clearances.Select(c => new ClearanceSummaryResponse
            {
                AuthorId = Convert.ToHexString(c.AuthorId).ToLowerInvariant(),
                OrganizationId = c.OrganizationId,
                MaxLevel = c.MaxLevel,
                Compartments = c.Compartments,
                Granted = c.Granted,
            }).ToList(),
        });
    }

    private static IResult FlushCache(IClearanceService clearanceService)
    {
        clearanceService.FlushAll();
        return Results.Ok(new { message = "Clearance cache flushed" });
    }
}
