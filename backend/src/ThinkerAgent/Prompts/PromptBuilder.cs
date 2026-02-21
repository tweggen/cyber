using System.Text;
using System.Text.Json;

namespace ThinkerAgent.Prompts;

public static class PromptBuilder
{
    public static string BuildPrompt(string jobType, JsonElement payload) => jobType switch
    {
        "DISTILL_CLAIMS" => BuildDistillPrompt(payload),
        "COMPARE_CLAIMS" => BuildComparePrompt(payload),
        "CLASSIFY_TOPIC" => BuildClassifyPrompt(payload),
        _ => throw new ArgumentException($"Unknown job type: {jobType}"),
    };

    public static string BuildDistillPrompt(JsonElement payload)
    {
        var content = payload.GetProperty("content").GetString()!;
        var maxClaims = payload.TryGetProperty("max_claims", out var mc) ? mc.GetInt32() : 12;

        var contextSection = "";
        if (payload.TryGetProperty("context_claims", out var contextClaims) &&
            contextClaims.ValueKind == JsonValueKind.Array &&
            contextClaims.GetArrayLength() > 0)
        {
            var sb = new StringBuilder();
            foreach (var c in contextClaims.EnumerateArray())
                sb.AppendLine($"- {c.GetProperty("text").GetString()}");

            contextSection = $$"""

CONTEXT — this document is part of a larger collection about:
{{sb.ToString().TrimEnd()}}

Focus on claims that ADD to this context, not repeat it.
""";
        }

        return $$"""
You are distilling a document into its top {{maxClaims}} factual claims.
{{contextSection}}
DOCUMENT:
{{content}}

Extract the top {{maxClaims}} most important factual claims from this document.
Each claim should be:
- A single declarative sentence
- Self-contained (understandable without the document)
- Specific (not vague or generic)
- Non-redundant with other claims in your list

Order by importance (most central claim first).

Respond as JSON only, no other text:
{
  "claims": [
    { "text": "...", "confidence": 0.95 },
    ...
  ]
}
""";
    }

    public static string BuildComparePrompt(JsonElement payload)
    {
        var claimsA = payload.GetProperty("claims_a");
        var claimsB = payload.GetProperty("claims_b");

        var existing = new StringBuilder();
        var idx = 1;
        foreach (var c in claimsA.EnumerateArray())
        {
            existing.AppendLine($"A{idx}. {c.GetProperty("text").GetString()}");
            idx++;
        }

        var newClaims = new StringBuilder();
        idx = 1;
        foreach (var c in claimsB.EnumerateArray())
        {
            newClaims.AppendLine($"B{idx}. {c.GetProperty("text").GetString()}");
            idx++;
        }

        return $$"""
You are comparing two sets of claims to measure novelty and contradiction.

EXISTING CLAIMS:
{{existing.ToString().TrimEnd()}}

NEW CLAIMS:
{{newClaims.ToString().TrimEnd()}}

For each NEW claim (B1, B2, ...), classify it as:
- NOVEL: covers a topic that no existing claim addresses
- REDUNDANT: semantically equivalent to an existing claim
- CONTRADICTS: makes a statement that conflicts with an existing claim

Be precise about contradiction vs. mere difference:
- "The earth is flat" vs "The earth is round" = CONTRADICTS (opposite conclusions)
- "The earth is round" vs "Mars is red" = NOVEL (different topics)
- "The earth is round" vs "The earth is approximately spherical" = REDUNDANT

Respond as JSON only, no other text:
{
  "classifications": [
    { "new_claim": 1, "type": "NOVEL" },
    { "new_claim": 2, "type": "REDUNDANT", "matches_existing": 4 },
    { "new_claim": 3, "type": "CONTRADICTS", "conflicts_with": 7, "severity": 0.8 }
  ]
}
""";
    }

    public static string BuildClassifyPrompt(JsonElement payload)
    {
        var claims = payload.GetProperty("claims");

        var claimsSb = new StringBuilder();
        foreach (var c in claims.EnumerateArray())
            claimsSb.AppendLine($"- {c.GetProperty("text").GetString()}");

        var topicsSb = new StringBuilder();
        if (payload.TryGetProperty("available_topics", out var topics) &&
            topics.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in topics.EnumerateArray())
                topicsSb.AppendLine($"- {t.GetString()}");
        }

        return $$"""
Given these claims from a document:
{{claimsSb.ToString().TrimEnd()}}

Which of these topics does this document best belong to?
{{topicsSb.ToString().TrimEnd()}}

If none fit well, suggest a new topic name.

Respond as JSON only, no other text:
{
  "primary_topic": "topic-name",
  "secondary_topics": [],
  "new_topic": null
}
""";
    }
}
