using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Core.Types;
using Notebook.Data;
using Notebook.Server.Auth;

namespace Notebook.Server.Endpoints;

public static class ObserveEndpoints
{
    public static void MapObserveEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/observe", Observe)
            .RequireAuthorization("CanRead");
        routes.MapGet("/notebooks/{notebookId}/participants", ListParticipants)
            .RequireAuthorization("CanRead");
    }

    private static async Task<IResult> Observe(
        Guid notebookId,
        [FromQuery] long? since,
        IAccessControl acl,
        NotebookDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireReadAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var query = db.Entries
            .Where(e => e.NotebookId == notebookId && e.FragmentOf == null);

        if (since.HasValue)
            query = query.Where(e => e.Sequence > since.Value);

        var entries = await query
            .OrderByDescending(e => e.Sequence)
            .Take(1000)
            .Select(e => new
            {
                e.Id,
                e.Topic,
                e.AuthorId,
                e.Sequence,
                e.Created,
                e.IntegrationCost,
                e.RevisionOf,
                e.IntegrationStatus,
            })
            .ToListAsync(ct);

        var notebook = await db.Notebooks.FirstOrDefaultAsync(n => n.Id == notebookId, ct);

        var changes = entries.Select(e => new ChangeEntryResponse
        {
            EntryId = e.Id,
            Operation = e.RevisionOf.HasValue ? "revise" : "write",
            Author = Convert.ToHexString(e.AuthorId).ToLowerInvariant(),
            Topic = e.Topic,
            IntegrationCost = e.IntegrationCost ?? new IntegrationCost
            {
                EntriesRevised = 0,
                ReferencesBroken = 0,
                CatalogShift = 0.0,
                Orphan = true,
            },
            CausalPosition = new CausalPositionResponse { Sequence = (ulong)e.Sequence },
            Created = e.Created.UtcDateTime,
            IntegrationStatus = e.IntegrationStatus.ToString().ToLowerInvariant(),
        }).ToList();

        return Results.Ok(new ObserveApiResponse
        {
            Changes = changes,
            NotebookEntropy = 0.0,
            CurrentSequence = (ulong)(notebook?.CurrentSequence ?? 0),
        });
    }

    private static async Task<IResult> ListParticipants(
        Guid notebookId,
        IAccessControl acl,
        NotebookDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var deny = await acl.RequireReadAsync(notebookId, authorId, ct);
        if (deny is not null) return deny;

        var access = await db.NotebookAccess
            .Where(a => a.NotebookId == notebookId)
            .ToListAsync(ct);

        var participants = access.Select(a => new ParticipantResponse
        {
            AuthorId = Convert.ToHexString(a.AuthorId).ToLowerInvariant(),
            Permissions = new PermissionsResponse { Read = a.Read, Write = a.Write },
            GrantedAt = a.Granted.UtcDateTime,
        }).ToList();

        return Results.Ok(new ParticipantsApiResponse { Participants = participants });
    }
}

// Response DTOs matching the Rust API shape expected by NotebookAdmin

internal sealed record ObserveApiResponse
{
    [JsonPropertyName("changes")]
    public required List<ChangeEntryResponse> Changes { get; init; }

    [JsonPropertyName("notebook_entropy")]
    public required double NotebookEntropy { get; init; }

    [JsonPropertyName("current_sequence")]
    public required ulong CurrentSequence { get; init; }
}

internal sealed record ChangeEntryResponse
{
    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("integration_cost")]
    public required IntegrationCost IntegrationCost { get; init; }

    [JsonPropertyName("causal_position")]
    public required CausalPositionResponse CausalPosition { get; init; }

    [JsonPropertyName("created")]
    public required DateTime Created { get; init; }

    [JsonPropertyName("integration_status")]
    public required string IntegrationStatus { get; init; }
}

internal sealed record CausalPositionResponse
{
    [JsonPropertyName("sequence")]
    public required ulong Sequence { get; init; }
}

internal sealed record ParticipantsApiResponse
{
    [JsonPropertyName("participants")]
    public required List<ParticipantResponse> Participants { get; init; }
}

internal sealed record ParticipantResponse
{
    [JsonPropertyName("author_id")]
    public required string AuthorId { get; init; }

    [JsonPropertyName("permissions")]
    public required PermissionsResponse Permissions { get; init; }

    [JsonPropertyName("granted_at")]
    public required DateTime GrantedAt { get; init; }
}

internal sealed record PermissionsResponse
{
    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("write")]
    public required bool Write { get; init; }
}
