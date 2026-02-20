using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Notebook.Core.Types;
using Notebook.Data;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;

namespace Notebook.Server.Endpoints;

public static class ReadEndpoints
{
    public static void MapReadEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/notebooks/{notebookId}/entries/{entryId}", ReadEntry)
            .RequireAuthorization("CanRead");
    }

    private static async Task<IResult> ReadEntry(
        Guid notebookId,
        Guid entryId,
        IAccessControl acl,
        IEntryRepository entryRepo,
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

        var entry = await entryRepo.GetEntryAsync(entryId, notebookId, ct);
        if (entry is null)
            return Results.NotFound(new { error = $"Entry {entryId} not found in notebook {notebookId}" });

        // Revisions: entries that are revisions of this entry
        var revisions = (await db.Entries
            .Where(e => e.RevisionOf == entryId && e.NotebookId == notebookId)
            .OrderBy(e => e.Sequence)
            .Select(e => new { e.Id, e.Topic, e.AuthorId, e.Created })
            .ToListAsync(ct))
            .Select(e => ToSummary(e.Id, e.Topic, e.AuthorId, e.Created))
            .ToList();

        // References: entries this entry references
        var references = new List<ReadEntrySummary>();
        if (entry.References.Count > 0)
        {
            references = (await db.Entries
                .Where(e => entry.References.Contains(e.Id) && e.NotebookId == notebookId)
                .Select(e => new { e.Id, e.Topic, e.AuthorId, e.Created })
                .ToListAsync(ct))
                .Select(e => ToSummary(e.Id, e.Topic, e.AuthorId, e.Created))
                .ToList();
        }

        // Fragments: entries that are fragments of this entry
        var fragments = (await db.Entries
            .Where(e => e.FragmentOf == entryId && e.NotebookId == notebookId)
            .OrderBy(e => e.FragmentIndex)
            .Select(e => new { e.Id, e.Topic, e.FragmentIndex, e.Claims, e.ClaimsStatus, e.Comparisons })
            .ToListAsync(ct))
            .Select(f => new ReadFragmentSummary
            {
                Id = f.Id,
                FragmentIndex = f.FragmentIndex ?? 0,
                Topic = f.Topic,
                Claims = f.Claims.Select(c => new ReadClaim { Text = c.Text, Confidence = c.Confidence }).ToList(),
                ClaimsStatus = f.ClaimsStatus.ToString().ToLowerInvariant(),
            })
            .ToList();

        // Referenced by: entries whose references array contains this entry's ID
        var referencedBy = (await db.Entries
            .FromSqlInterpolated(
                $"""SELECT * FROM entries WHERE notebook_id = {notebookId} AND {entryId} = ANY("references")""")
            .Select(e => new { e.Id, e.Topic, e.AuthorId, e.Created })
            .ToListAsync(ct))
            .Select(e => ToSummary(e.Id, e.Topic, e.AuthorId, e.Created))
            .ToList();

        var cost = entry.IntegrationCost ?? new IntegrationCost
        {
            EntriesRevised = 0,
            ReferencesBroken = 0,
            CatalogShift = 0.0,
            Orphan = true,
        };

        var response = new ReadEntryApiResponse
        {
            Entry = new ReadEntryDetail
            {
                Id = entry.Id,
                Content = Encoding.UTF8.GetString(entry.Content),
                ContentType = entry.ContentType,
                Topic = entry.Topic,
                Author = Convert.ToHexString(entry.AuthorId).ToLowerInvariant(),
                References = entry.References,
                RevisionOf = entry.RevisionOf,
                CausalPosition = new ReadCausalPosition { Sequence = (ulong)entry.Sequence },
                Created = entry.Created.UtcDateTime,
                IntegrationCost = cost,
                Claims = entry.Claims.Select(c => new ReadClaim
                {
                    Text = c.Text,
                    Confidence = c.Confidence,
                }).ToList(),
                ClaimsStatus = entry.ClaimsStatus.ToString().ToLowerInvariant(),
                Comparisons = entry.Comparisons.Select(c => new ReadComparison
                {
                    ComparedAgainst = c.ComparedAgainst ?? Guid.Empty,
                    Entropy = c.Entropy,
                    Friction = c.Friction,
                    Contradictions = c.Contradictions.Select(x => new ReadContradiction
                    {
                        ClaimA = x.ClaimA,
                        ClaimB = x.ClaimB,
                        Severity = x.Severity,
                    }).ToList(),
                    ComputedAt = c.ComputedAt?.UtcDateTime ?? DateTime.MinValue,
                }).ToList(),
                MaxFriction = entry.MaxFriction,
                NeedsReview = entry.NeedsReview,
                FragmentOf = entry.FragmentOf,
                FragmentIndex = entry.FragmentIndex,
                IntegrationStatus = entry.IntegrationStatus.ToString().ToLowerInvariant(),
            },
            Revisions = revisions,
            References = references,
            ReferencedBy = referencedBy,
            Fragments = fragments,
        };

        return Results.Ok(response);
    }

    private static ReadEntrySummary ToSummary(Guid id, string? topic, byte[] authorId, DateTimeOffset created) =>
        new()
        {
            Id = id,
            Topic = topic,
            Author = Convert.ToHexString(authorId).ToLowerInvariant(),
            Created = created,
        };
}

// ── Response DTOs matching the shape expected by the admin client ──

internal sealed record ReadEntryApiResponse
{
    [JsonPropertyName("entry")]
    public required ReadEntryDetail Entry { get; init; }

    [JsonPropertyName("revisions")]
    public required List<ReadEntrySummary> Revisions { get; init; }

    [JsonPropertyName("references")]
    public required List<ReadEntrySummary> References { get; init; }

    [JsonPropertyName("referenced_by")]
    public required List<ReadEntrySummary> ReferencedBy { get; init; }

    [JsonPropertyName("fragments")]
    public required List<ReadFragmentSummary> Fragments { get; init; }
}

internal sealed record ReadEntryDetail
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("content_type")]
    public required string ContentType { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("references")]
    public required List<Guid> References { get; init; }

    [JsonPropertyName("revision_of")]
    public Guid? RevisionOf { get; init; }

    [JsonPropertyName("causal_position")]
    public required ReadCausalPosition CausalPosition { get; init; }

    [JsonPropertyName("created")]
    public required DateTime Created { get; init; }

    [JsonPropertyName("integration_cost")]
    public required IntegrationCost IntegrationCost { get; init; }

    [JsonPropertyName("claims")]
    public required List<ReadClaim> Claims { get; init; }

    [JsonPropertyName("claims_status")]
    public required string ClaimsStatus { get; init; }

    [JsonPropertyName("comparisons")]
    public required List<ReadComparison> Comparisons { get; init; }

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; init; }

    [JsonPropertyName("needs_review")]
    public required bool NeedsReview { get; init; }

    [JsonPropertyName("fragment_of")]
    public Guid? FragmentOf { get; init; }

    [JsonPropertyName("fragment_index")]
    public int? FragmentIndex { get; init; }

    [JsonPropertyName("integration_status")]
    public required string IntegrationStatus { get; init; }
}

internal sealed record ReadCausalPosition
{
    [JsonPropertyName("sequence")]
    public required ulong Sequence { get; init; }
}

internal sealed record ReadClaim
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}

internal sealed record ReadComparison
{
    [JsonPropertyName("compared_against")]
    public required Guid ComparedAgainst { get; init; }

    [JsonPropertyName("entropy")]
    public required double Entropy { get; init; }

    [JsonPropertyName("friction")]
    public required double Friction { get; init; }

    [JsonPropertyName("contradictions")]
    public required List<ReadContradiction> Contradictions { get; init; }

    [JsonPropertyName("computed_at")]
    public required DateTime ComputedAt { get; init; }
}

internal sealed record ReadContradiction
{
    [JsonPropertyName("claim_a")]
    public required string ClaimA { get; init; }

    [JsonPropertyName("claim_b")]
    public required string ClaimB { get; init; }

    [JsonPropertyName("severity")]
    public required double Severity { get; init; }
}

internal sealed record ReadEntrySummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }
}

internal sealed record ReadFragmentSummary
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("fragment_index")]
    public required int FragmentIndex { get; init; }

    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    [JsonPropertyName("claims")]
    public required List<ReadClaim> Claims { get; init; }

    [JsonPropertyName("claims_status")]
    public required string ClaimsStatus { get; init; }
}
