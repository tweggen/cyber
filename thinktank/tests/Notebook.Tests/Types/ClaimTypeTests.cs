using System.Text.Json;
using Notebook.Core.Types;

namespace Notebook.Tests.Types;

public class ClaimTypeTests
{
    [Fact]
    public void Claim_Roundtrip()
    {
        var claim = new Claim { Text = "The sky is blue", Confidence = 0.95 };
        var json = JsonSerializer.Serialize(claim);
        var parsed = JsonSerializer.Deserialize<Claim>(json)!;
        Assert.Equal(claim.Text, parsed.Text);
        Assert.Equal(claim.Confidence, parsed.Confidence);
    }

    [Theory]
    [InlineData(ClaimsStatus.Pending, "\"pending\"")]
    [InlineData(ClaimsStatus.Distilled, "\"distilled\"")]
    [InlineData(ClaimsStatus.Verified, "\"verified\"")]
    public void ClaimsStatus_Roundtrip(ClaimsStatus status, string expectedJson)
    {
        var json = JsonSerializer.Serialize(status);
        Assert.Equal(expectedJson, json);
        var parsed = JsonSerializer.Deserialize<ClaimsStatus>(json);
        Assert.Equal(status, parsed);
    }

    [Fact]
    public void ClaimComparison_Roundtrip()
    {
        var comp = new ClaimComparison
        {
            ComparedAgainst = Guid.NewGuid(),
            Entropy = 0.58,
            Friction = 0.17,
            Contradictions =
            [
                new Contradiction
                {
                    ClaimA = "Deploys go to staging",
                    ClaimB = "Production skips staging",
                    Severity = 0.9,
                }
            ],
            ComputedAt = DateTimeOffset.UtcNow,
            ComputedBy = "robot-haiku-1",
        };
        var json = JsonSerializer.Serialize(comp);
        var parsed = JsonSerializer.Deserialize<ClaimComparison>(json)!;
        Assert.Equal(comp.Entropy, parsed.Entropy);
        Assert.Equal(comp.Friction, parsed.Friction);
        Assert.Single(parsed.Contradictions);
    }

    [Fact]
    public void JobType_Serde()
    {
        var jt = JobType.DistillClaims;
        var json = JsonSerializer.Serialize(jt);
        Assert.Equal("\"DISTILL_CLAIMS\"", json);
        var parsed = JsonSerializer.Deserialize<JobType>(json);
        Assert.Equal(jt, parsed);
    }

    [Fact]
    public void JobStatus_Serde()
    {
        var js = JobStatus.InProgress;
        var json = JsonSerializer.Serialize(js);
        Assert.Equal("\"in_progress\"", json);
        var parsed = JsonSerializer.Deserialize<JobStatus>(json);
        Assert.Equal(js, parsed);
    }
}
