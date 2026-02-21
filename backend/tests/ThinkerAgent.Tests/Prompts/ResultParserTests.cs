using System.Text.Json;
using ThinkerAgent.Prompts;

namespace ThinkerAgent.Tests.Prompts;

public class ResultParserTests
{
    [Fact]
    public void ParseDistillResult_ValidJson()
    {
        var response = """{"claims": [{"text": "Test claim", "confidence": 0.9}]}""";
        var result = ResultParser.ParseDistillResult(response);

        var claims = (List<Dictionary<string, object>>)result["claims"];
        Assert.Single(claims);
        Assert.Equal("Test claim", claims[0]["text"]);
    }

    [Fact]
    public void ParseDistillResult_CodeBlock()
    {
        var response = "```json\n{\"claims\": [{\"text\": \"Test\", \"confidence\": 0.9}]}\n```";
        var result = ResultParser.ParseDistillResult(response);

        var claims = (List<Dictionary<string, object>>)result["claims"];
        Assert.Single(claims);
    }

    [Fact]
    public void ParseDistillResult_InvalidJson_Throws()
    {
        Assert.Throws<FormatException>(() => ResultParser.ParseDistillResult("not json at all"));
    }

    [Fact]
    public void ParseCompareResult_AllNovel()
    {
        var response = """{"classifications": [{"new_claim": 1, "type": "NOVEL"}]}""";
        var payload = JsonSerializer.SerializeToElement(new
        {
            compare_against_id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            claims_a = new[] { new { text = "A", confidence = 0.9 } },
            claims_b = new[] { new { text = "B", confidence = 0.9 } },
        });

        var result = ResultParser.ParseCompareResult(response, payload);

        Assert.Equal(1.0, result["entropy"]);
        Assert.Equal(0.0, result["friction"]);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", result["compared_against"]);
    }

    [Fact]
    public void ParseCompareResult_WithContradiction()
    {
        var response = JsonSerializer.Serialize(new
        {
            classifications = new[]
            {
                new { new_claim = 1, type = "CONTRADICTS", conflicts_with = 1, severity = 0.9 },
            },
        });
        var payload = JsonSerializer.SerializeToElement(new
        {
            compare_against_id = "11111111-2222-3333-4444-555555555555",
            claims_a = new[] { new { text = "Earth is round", confidence = 0.9 } },
            claims_b = new[] { new { text = "Earth is flat", confidence = 0.8 } },
        });

        var result = ResultParser.ParseCompareResult(response, payload);

        Assert.Equal(0.0, result["entropy"]);
        Assert.Equal(1.0, result["friction"]);
        Assert.Equal("11111111-2222-3333-4444-555555555555", result["compared_against"]);
        var contradictions = (List<Dictionary<string, object>>)result["contradictions"]!;
        Assert.Single(contradictions);
        Assert.Equal("Earth is round", contradictions[0]["claim_a"]);
        Assert.Equal("Earth is flat", contradictions[0]["claim_b"]);
        Assert.Equal(0.9, contradictions[0]["severity"]);
    }

    [Fact]
    public void ParseCompareResult_Mixed()
    {
        var response = JsonSerializer.Serialize(new
        {
            classifications = new object[]
            {
                new { new_claim = 1, type = "NOVEL" },
                new { new_claim = 2, type = "REDUNDANT", matches_existing = 1 },
                new { new_claim = 3, type = "CONTRADICTS", conflicts_with = 1, severity = 0.7 },
            },
        });
        var payload = JsonSerializer.SerializeToElement(new
        {
            compare_against_id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            claims_a = new[] { new { text = "A1", confidence = 0.9 } },
            claims_b = new[]
            {
                new { text = "B1", confidence = 0.9 },
                new { text = "B2", confidence = 0.8 },
                new { text = "B3", confidence = 0.7 },
            },
        });

        var result = ResultParser.ParseCompareResult(response, payload);

        Assert.Equal(Math.Round(1.0 / 3, 4), result["entropy"]);
        Assert.Equal(Math.Round(1.0 / 3, 4), result["friction"]);
        var contradictions = (List<Dictionary<string, object>>)result["contradictions"]!;
        Assert.Single(contradictions);
    }

    [Fact]
    public void ParseClassifyResult_Valid()
    {
        var response = """{"primary_topic": "devops", "secondary_topics": [], "new_topic": null}""";
        var result = ResultParser.ParseClassifyResult(response);

        Assert.Equal("devops", result["primary_topic"]);
        Assert.Empty((List<string>)result["secondary_topics"]!);
        Assert.Null(result["new_topic"]);
    }

    [Fact]
    public void ExtractJson_PlainJson()
    {
        var element = ResultParser.ExtractJson("""{"key": "value"}""");
        Assert.Equal("value", element.GetProperty("key").GetString());
    }

    [Fact]
    public void ExtractJson_CodeBlock()
    {
        var text = "```json\n{\"key\": \"value\"}\n```";
        var element = ResultParser.ExtractJson(text);
        Assert.Equal("value", element.GetProperty("key").GetString());
    }

    [Fact]
    public void ExtractJson_CodeBlockNoLang()
    {
        var text = "```\n{\"key\": \"value\"}\n```";
        var element = ResultParser.ExtractJson(text);
        Assert.Equal("value", element.GetProperty("key").GetString());
    }
}
