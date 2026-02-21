using System.Text.Json;
using ThinkerAgent.Prompts;

namespace ThinkerAgent.Tests.Prompts;

public class PromptBuilderTests
{
    [Fact]
    public void BuildDistillPrompt_Basic()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            content = "OAuth tokens are validated.",
            max_claims = 6,
        });

        var prompt = PromptBuilder.BuildDistillPrompt(payload);

        Assert.Contains("OAuth tokens", prompt);
        Assert.Contains("top 6", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void BuildDistillPrompt_WithContext()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            content = "Details about validation.",
            max_claims = 12,
            context_claims = new[] { new { text = "System uses OAuth", confidence = 0.9 } },
        });

        var prompt = PromptBuilder.BuildDistillPrompt(payload);

        Assert.Contains("System uses OAuth", prompt);
        Assert.Contains("CONTEXT", prompt);
    }

    [Fact]
    public void BuildDistillPrompt_DefaultMaxClaims()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            content = "Some content.",
        });

        var prompt = PromptBuilder.BuildDistillPrompt(payload);

        Assert.Contains("top 12", prompt);
    }

    [Fact]
    public void BuildComparePrompt_FormatsClaimsCorrectly()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            claims_a = new[] { new { text = "Earth is round", confidence = 0.9 } },
            claims_b = new[] { new { text = "Earth is flat", confidence = 0.8 } },
        });

        var prompt = PromptBuilder.BuildComparePrompt(payload);

        Assert.Contains("A1. Earth is round", prompt);
        Assert.Contains("B1. Earth is flat", prompt);
    }

    [Fact]
    public void BuildClassifyPrompt_IncludesClaimsAndTopics()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            claims = new[] { new { text = "OAuth is used for auth", confidence = 0.9 } },
            available_topics = new[] { "auth", "devops", "frontend" },
        });

        var prompt = PromptBuilder.BuildClassifyPrompt(payload);

        Assert.Contains("OAuth is used for auth", prompt);
        Assert.Contains("auth", prompt);
        Assert.Contains("devops", prompt);
    }

    [Fact]
    public void BuildPrompt_ThrowsForUnknownJobType()
    {
        var payload = JsonSerializer.SerializeToElement(new { });
        Assert.Throws<ArgumentException>(() => PromptBuilder.BuildPrompt("UNKNOWN", payload));
    }
}
