using System.Text.Json;
using Notebook.Core.Types;

namespace Notebook.Tests.Types;

public class EntryTypeTests
{
    [Fact]
    public void Entry_WithClaims_Roundtrip()
    {
        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            NotebookId = Guid.NewGuid(),
            Content = "hello"u8.ToArray(),
            ContentType = "text/plain",
            AuthorId = new byte[] { 1, 2, 3 },
            Signature = new byte[] { 4, 5, 6 },
            Sequence = 42,
            Created = DateTimeOffset.UtcNow,
            Claims = [new Claim { Text = "Test claim", Confidence = 0.9 }],
            ClaimsStatus = ClaimsStatus.Distilled,
        };
        var json = JsonSerializer.Serialize(entry);
        var parsed = JsonSerializer.Deserialize<Entry>(json)!;
        Assert.Single(parsed.Claims);
        Assert.Equal("Test claim", parsed.Claims[0].Text);
        Assert.Equal(ClaimsStatus.Distilled, parsed.ClaimsStatus);
    }

    [Fact]
    public void Entry_DefaultValues()
    {
        var entry = new Entry();
        Assert.Empty(entry.Claims);
        Assert.Equal(ClaimsStatus.Pending, entry.ClaimsStatus);
        Assert.Null(entry.FragmentOf);
        Assert.Null(entry.FragmentIndex);
        Assert.Empty(entry.Comparisons);
        Assert.Null(entry.MaxFriction);
        Assert.False(entry.NeedsReview);
        Assert.Empty(entry.References);
        Assert.Null(entry.IntegrationCost);
    }

    [Fact]
    public void Entry_V1Fields_Roundtrip()
    {
        var id = Guid.NewGuid();
        var notebookId = Guid.NewGuid();
        var revisionOf = Guid.NewGuid();
        var refId = Guid.NewGuid();

        var entry = new Entry
        {
            Id = id,
            NotebookId = notebookId,
            Content = "test content"u8.ToArray(),
            ContentType = "text/markdown",
            Topic = "architecture",
            AuthorId = new byte[] { 10, 20, 30 },
            Signature = new byte[] { 40, 50, 60 },
            RevisionOf = revisionOf,
            References = [refId],
            Sequence = 7,
            Created = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(entry);
        var parsed = JsonSerializer.Deserialize<Entry>(json)!;

        Assert.Equal(id, parsed.Id);
        Assert.Equal(notebookId, parsed.NotebookId);
        Assert.Equal("text/markdown", parsed.ContentType);
        Assert.Equal("architecture", parsed.Topic);
        Assert.Equal(revisionOf, parsed.RevisionOf);
        Assert.Equal([refId], parsed.References);
        Assert.Equal(7, parsed.Sequence);
    }

    [Fact]
    public void IntegrationCost_Roundtrip()
    {
        var cost = new IntegrationCost
        {
            EntriesRevised = 3,
            ReferencesBroken = 1,
            CatalogShift = 0.42,
            Orphan = false,
        };
        var json = JsonSerializer.Serialize(cost);
        var parsed = JsonSerializer.Deserialize<IntegrationCost>(json)!;
        Assert.Equal(cost.EntriesRevised, parsed.EntriesRevised);
        Assert.Equal(cost.ReferencesBroken, parsed.ReferencesBroken);
        Assert.Equal(cost.CatalogShift, parsed.CatalogShift);
        Assert.Equal(cost.Orphan, parsed.Orphan);
    }
}
