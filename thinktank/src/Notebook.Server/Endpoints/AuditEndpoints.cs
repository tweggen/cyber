using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Server.Models;

namespace Notebook.Server.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/audit", QueryAuditLog)
            .RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> QueryAuditLog(
        [FromQuery] string? action,
        [FromQuery] string? resource,
        [FromQuery] string? actor,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        NotebookDbContext db = null!,
        CancellationToken ct = default)
    {
        limit = Math.Min(limit, 200);

        var query = db.AuditLog.AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrEmpty(resource))
            query = query.Where(a => a.Resource == resource);

        if (!string.IsNullOrEmpty(actor))
        {
            var actorBytes = Convert.FromHexString(actor);
            query = query.Where(a => a.Actor == actorBytes);
        }

        var entries = await query
            .OrderByDescending(a => a.Created)
            .Skip(offset)
            .Take(limit)
            .Select(a => new AuditEntryResponse
            {
                Id = a.Id,
                Actor = Convert.ToHexString(a.Actor).ToLower(),
                Action = a.Action,
                Resource = a.Resource,
                Detail = a.Detail,
                Ip = a.Ip,
                UserAgent = a.UserAgent,
                Created = a.Created,
            })
            .ToListAsync(ct);

        return Results.Ok(new AuditListResponse
        {
            Entries = entries,
            Count = entries.Count,
        });
    }
}
