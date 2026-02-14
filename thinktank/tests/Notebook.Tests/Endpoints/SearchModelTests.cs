using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class SearchModelTests
{
    [Fact]
    public void SearchResponse_Serialize()
    {
        var response = new SearchResponse
        {
            Results =
            [
                new SearchResult
                {
                    EntryId = Guid.NewGuid(),
                    Topic = "auth/oauth",
                    Snippet = "OAuth tokens are validated...",
                    MatchLocation = "content",
                    RelevanceScore = 0.85,
                },
            ],
        };
        var json = JsonSerializer.Serialize(response);
        Assert.Contains("entry_id", json);
        Assert.Contains("relevance_score", json);
        Assert.Contains("match_location", json);
    }

    [Fact]
    public void SearchResult_Roundtrip()
    {
        var result = new SearchResult
        {
            EntryId = Guid.NewGuid(),
            Topic = "test/topic",
            Snippet = "some matched text",
            MatchLocation = "claims",
            RelevanceScore = 0.72,
        };
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonSerializer.Deserialize<SearchResult>(json)!;
        Assert.Equal(result.EntryId, parsed.EntryId);
        Assert.Equal("claims", parsed.MatchLocation);
        Assert.Equal(0.72, parsed.RelevanceScore);
    }

    [Fact]
    public void BrowseEntry_Roundtrip()
    {
        var entry = new BrowseEntry
        {
            Id = Guid.NewGuid(),
            Topic = "test/browse",
            ClaimsStatus = "distilled",
            MaxFriction = 0.15,
            NeedsReview = false,
            Sequence = 42,
            Created = DateTimeOffset.UtcNow,
            AuthorId = "abcd1234",
            ClaimCount = 3,
        };
        var json = JsonSerializer.Serialize(entry);
        var parsed = JsonSerializer.Deserialize<BrowseEntry>(json)!;
        Assert.Equal(entry.Id, parsed.Id);
        Assert.Equal("distilled", parsed.ClaimsStatus);
        Assert.Equal(3, parsed.ClaimCount);
        Assert.Equal(0.15, parsed.MaxFriction);
    }
}
