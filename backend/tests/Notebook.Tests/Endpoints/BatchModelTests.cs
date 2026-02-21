using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class BatchModelTests
{
    [Fact]
    public void BatchWriteRequest_Deserialize()
    {
        var json = """
        {
            "entries": [
                {"content": "entry 1", "topic": "test"},
                {"content": "entry 2", "content_type": "text/markdown"}
            ],
            "author": "bulk-import"
        }
        """;

        var request = JsonSerializer.Deserialize<BatchWriteRequest>(json)!;
        Assert.Equal(2, request.Entries.Count);
        Assert.Equal("entry 1", request.Entries[0].Content);
        Assert.Equal("test", request.Entries[0].Topic);
        Assert.Null(request.Entries[0].ContentType);
        Assert.Equal("text/markdown", request.Entries[1].ContentType);
        Assert.Equal("bulk-import", request.Author);
    }

    [Fact]
    public void BatchWriteResponse_Serialize()
    {
        var response = new BatchWriteResponse
        {
            Results =
            [
                new BatchEntryResult
                {
                    EntryId = Guid.Empty,
                    CausalPosition = 42,
                    IntegrationCost = 0.0,
                    ClaimsStatus = ClaimsStatus.Pending,
                }
            ],
            JobsCreated = 1,
        };

        var json = JsonSerializer.Serialize(response);
        var parsed = JsonSerializer.Deserialize<BatchWriteResponse>(json)!;
        Assert.Single(parsed.Results);
        Assert.Equal(42, parsed.Results[0].CausalPosition);
        Assert.Equal(1, parsed.JobsCreated);
    }

    [Fact]
    public void UpdateClaimsRequest_Deserialize()
    {
        var json = """
        {
            "claims": [
                {"text": "OAuth tokens validated before jobs", "confidence": 0.95},
                {"text": "Validation uses refresh endpoint", "confidence": 0.82}
            ],
            "author": "robot-haiku-1"
        }
        """;

        var request = JsonSerializer.Deserialize<UpdateClaimsRequest>(json)!;
        Assert.Equal(2, request.Claims.Count);
        Assert.Equal(0.95, request.Claims[0].Confidence);
        Assert.Equal("robot-haiku-1", request.Author);
    }

    [Fact]
    public void UpdateClaimsResponse_Roundtrip()
    {
        var response = new UpdateClaimsResponse
        {
            EntryId = Guid.NewGuid(),
            ClaimsStatus = ClaimsStatus.Distilled,
            ComparisonJobsCreated = 3,
        };

        var json = JsonSerializer.Serialize(response);
        var parsed = JsonSerializer.Deserialize<UpdateClaimsResponse>(json)!;
        Assert.Equal(response.EntryId, parsed.EntryId);
        Assert.Equal(ClaimsStatus.Distilled, parsed.ClaimsStatus);
        Assert.Equal(3, parsed.ComparisonJobsCreated);
    }

    [Fact]
    public void BatchEntryRequest_WithFragments_Deserialize()
    {
        var parentId = Guid.NewGuid();
        var json = $$"""
        {
            "content": "fragment content",
            "fragment_of": "{{parentId}}",
            "fragment_index": 2
        }
        """;

        var request = JsonSerializer.Deserialize<BatchEntryRequest>(json)!;
        Assert.Equal(parentId, request.FragmentOf);
        Assert.Equal(2, request.FragmentIndex);
    }
}
