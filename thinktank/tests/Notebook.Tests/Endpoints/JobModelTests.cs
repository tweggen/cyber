using System.Text.Json;
using Notebook.Server.Models;

namespace Notebook.Tests.Endpoints;

public class JobModelTests
{
    [Fact]
    public void CompleteJobRequest_Deserialize()
    {
        var json = """
        {
            "worker_id": "robot-1",
            "result": {
                "claims": [
                    {"text": "Test claim", "confidence": 0.9}
                ]
            }
        }
        """;

        var request = JsonSerializer.Deserialize<CompleteJobRequest>(json)!;
        Assert.Equal("robot-1", request.WorkerId);
        Assert.True(request.Result.TryGetProperty("claims", out _));
    }

    [Fact]
    public void FailJobRequest_Deserialize()
    {
        var json = """{"worker_id": "robot-1", "error": "LLM returned invalid JSON"}""";
        var request = JsonSerializer.Deserialize<FailJobRequest>(json)!;
        Assert.Equal("robot-1", request.WorkerId);
        Assert.Equal("LLM returned invalid JSON", request.Error);
    }

    [Fact]
    public void JobStatsResponse_Serialize()
    {
        var stats = new JobStatsResponse
        {
            DistillClaims = new JobTypeStats { Pending = 5, InProgress = 2, Completed = 10, Failed = 1 },
            CompareClaims = new JobTypeStats(),
            ClassifyTopic = new JobTypeStats(),
            EmbedClaims = new JobTypeStats(),
        };
        var json = JsonSerializer.Serialize(stats);
        Assert.Contains("DISTILL_CLAIMS", json);
        Assert.Contains("COMPARE_CLAIMS", json);
        Assert.Contains("CLASSIFY_TOPIC", json);
        Assert.Contains("EMBED_CLAIMS", json);
    }

    [Fact]
    public void JobResponse_Roundtrip()
    {
        var payload = JsonSerializer.SerializeToDocument(new { entry_id = Guid.NewGuid().ToString() });
        var response = new JobResponse
        {
            Id = Guid.NewGuid(),
            JobType = "DISTILL_CLAIMS",
            Status = "in_progress",
            Payload = payload.RootElement,
            Created = DateTimeOffset.UtcNow,
            ClaimedAt = DateTimeOffset.UtcNow,
            ClaimedBy = "robot-1",
        };

        var json = JsonSerializer.Serialize(response);
        var parsed = JsonSerializer.Deserialize<JobResponse>(json)!;
        Assert.Equal(response.Id, parsed.Id);
        Assert.Equal("DISTILL_CLAIMS", parsed.JobType);
        Assert.Equal("robot-1", parsed.ClaimedBy);
    }

    [Fact]
    public void JobStatsResponse_Deserialize_Roundtrip()
    {
        var stats = new JobStatsResponse
        {
            DistillClaims = new JobTypeStats { Pending = 5, InProgress = 2, Completed = 10, Failed = 1 },
            CompareClaims = new JobTypeStats { Pending = 0, InProgress = 0, Completed = 3, Failed = 0 },
            ClassifyTopic = new JobTypeStats(),
            EmbedClaims = new JobTypeStats { Pending = 1 },
        };
        var json = JsonSerializer.Serialize(stats);
        var parsed = JsonSerializer.Deserialize<JobStatsResponse>(json)!;
        Assert.Equal(5, parsed.DistillClaims.Pending);
        Assert.Equal(2, parsed.DistillClaims.InProgress);
        Assert.Equal(3, parsed.CompareClaims.Completed);
        Assert.Equal(0, parsed.ClassifyTopic.Pending);
        Assert.Equal(1, parsed.EmbedClaims.Pending);
    }
}
