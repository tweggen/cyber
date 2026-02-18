using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThinkerAgent.Prompts;

public static class ResultParser
{
    public static object ParseResult(string jobType, string response, JsonElement payload) => jobType switch
    {
        "DISTILL_CLAIMS" => ParseDistillResult(response),
        "COMPARE_CLAIMS" => (object)ParseCompareResult(response, payload),
        "CLASSIFY_TOPIC" => (object)ParseClassifyResult(response),
        _ => throw new ArgumentException($"Unknown job type: {jobType}"),
    };

    public static Dictionary<string, object> ParseDistillResult(string response)
    {
        JsonElement data;
        try
        {
            data = ExtractJson(response);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Failed to parse distill result: {ex.Message}\nResponse: {Truncate(response)}", ex);
        }

        if (!data.TryGetProperty("claims", out var claimsArr) || claimsArr.ValueKind != JsonValueKind.Array)
            throw new FormatException($"Failed to parse distill result: missing 'claims' array\nResponse: {Truncate(response)}");

        var claims = new List<Dictionary<string, object>>();
        foreach (var claim in claimsArr.EnumerateArray())
        {
            if (!claim.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                throw new FormatException($"Failed to parse distill result: claim missing text\nResponse: {Truncate(response)}");
            if (!claim.TryGetProperty("confidence", out var conf) || (conf.ValueKind != JsonValueKind.Number))
                throw new FormatException($"Failed to parse distill result: claim missing confidence\nResponse: {Truncate(response)}");

            claims.Add(new Dictionary<string, object>
            {
                ["text"] = text.GetString()!,
                ["confidence"] = conf.GetDouble(),
            });
        }

        return new Dictionary<string, object> { ["claims"] = claims };
    }

    public static Dictionary<string, object?> ParseCompareResult(string response, JsonElement payload)
    {
        JsonElement data;
        try
        {
            data = ExtractJson(response);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Failed to parse compare result: {ex.Message}\nResponse: {Truncate(response)}", ex);
        }

        if (!data.TryGetProperty("classifications", out var classArr) || classArr.ValueKind != JsonValueKind.Array)
            throw new FormatException($"Failed to parse compare result: missing 'classifications'\nResponse: {Truncate(response)}");

        var claimsA = payload.GetProperty("claims_a");
        var claimsB = payload.GetProperty("claims_b");
        var n = claimsB.GetArrayLength();

        var novelCount = 0;
        var contradictCount = 0;
        var contradictions = new List<Dictionary<string, object>>();

        foreach (var c in classArr.EnumerateArray())
        {
            var type = c.GetProperty("type").GetString();
            if (type == "NOVEL")
                novelCount++;
            else if (type == "CONTRADICTS")
            {
                contradictCount++;

                var newIdx = c.TryGetProperty("new_claim", out var nc) ? nc.GetInt32() - 1 : 0;
                var existingIdx = c.TryGetProperty("conflicts_with", out var cw) ? cw.GetInt32() - 1 : 0;
                var severity = c.TryGetProperty("severity", out var sev) ? sev.GetDouble() : 0.5;

                var claimAText = existingIdx >= 0 && existingIdx < claimsA.GetArrayLength()
                    ? claimsA[existingIdx].GetProperty("text").GetString()!
                    : "?";
                var claimBText = newIdx >= 0 && newIdx < claimsB.GetArrayLength()
                    ? claimsB[newIdx].GetProperty("text").GetString()!
                    : "?";

                contradictions.Add(new Dictionary<string, object>
                {
                    ["claim_a"] = claimAText,
                    ["claim_b"] = claimBText,
                    ["severity"] = severity,
                });
            }
        }

        var entropy = n > 0 ? Math.Round((double)novelCount / n, 4) : 0.0;
        var friction = n > 0 ? Math.Round((double)contradictCount / n, 4) : 0.0;

        var compareAgainstId = payload.TryGetProperty("compare_against_id", out var caid)
            ? caid.GetString()
            : null;

        return new Dictionary<string, object?>
        {
            ["compared_against"] = compareAgainstId,
            ["entropy"] = entropy,
            ["friction"] = friction,
            ["contradictions"] = contradictions,
        };
    }

    public static Dictionary<string, object?> ParseClassifyResult(string response)
    {
        JsonElement data;
        try
        {
            data = ExtractJson(response);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Failed to parse classify result: {ex.Message}\nResponse: {Truncate(response)}", ex);
        }

        var primaryTopic = data.TryGetProperty("primary_topic", out var pt) ? pt.GetString() ?? "" : "";

        var secondaryTopics = new List<string>();
        if (data.TryGetProperty("secondary_topics", out var st) && st.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in st.EnumerateArray())
                if (t.ValueKind == JsonValueKind.String)
                    secondaryTopics.Add(t.GetString()!);
        }

        string? newTopic = null;
        if (data.TryGetProperty("new_topic", out var nt) && nt.ValueKind == JsonValueKind.String)
            newTopic = nt.GetString();

        return new Dictionary<string, object?>
        {
            ["primary_topic"] = primaryTopic,
            ["secondary_topics"] = secondaryTopics,
            ["new_topic"] = newTopic,
        };
    }

    public static JsonElement ExtractJson(string text)
    {
        text = text.Trim();

        // Extract content between ```json ... ``` fences if present
        var fenceMatch = Regex.Match(text, @"```(?:json)?\s*\n([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups[1].Value.Trim();
        }

        // Find the outermost JSON object or array by brace matching
        var startIdx = text.IndexOfAny(['{', '[']);
        if (startIdx >= 0)
        {
            var open = text[startIdx];
            var close = open == '{' ? '}' : ']';
            var depth = 0;
            var inString = false;
            var escape = false;

            for (var i = startIdx; i < text.Length; i++)
            {
                var c = text[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == open) depth++;
                else if (c == close) { depth--; if (depth == 0) { text = text[startIdx..(i + 1)]; break; } }
            }
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string Truncate(string s, int max = 500) =>
        s.Length <= max ? s : s[..max];
}
