using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThinkerAgent.Services;

public sealed class OpenAiLlmClient : ILlmClient
{
    private readonly HttpClient _http;

    public OpenAiLlmClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> IsRunningAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync("v1/models", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<LlmModel>> ListModelsAsync(CancellationToken ct)
    {
        var resp = await _http.GetAsync("v1/models", ct);
        resp.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var models = new List<LlmModel>();

        if (doc.RootElement.TryGetProperty("data", out var arr))
        {
            foreach (var m in arr.EnumerateArray())
            {
                var id = m.GetProperty("id").GetString() ?? "";
                var created = m.TryGetProperty("created", out var c) ? c.GetInt64() : 0;
                var createdAt = created > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(created).ToString("o")
                    : "";
                models.Add(new LlmModel(id, createdAt, 0));
            }
        }

        return models;
    }

    public async Task<LlmChatResponse> ChatAsync(string model, string prompt, int maxTokens,
        IProgress<int>? tokenProgress = null, CancellationToken ct = default)
    {
        var request = new OpenAiChatRequest
        {
            Model = model,
            Messages = [new OpenAiChatMessage { Role = "user", Content = prompt }],
            MaxTokens = maxTokens,
            Stream = true,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(request),
        };

        using var resp = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var contentBuilder = new System.Text.StringBuilder();
        var tokenCount = 0;
        var sw = Stopwatch.StartNew();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // SSE format: "data: {...}" or "data: [DONE]"
            if (!line.StartsWith("data: "))
                continue;

            var payload = line.Substring(6);
            if (payload == "[DONE]")
                break;

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices))
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var chunk))
                    {
                        var text = chunk.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            contentBuilder.Append(text);
                            tokenCount++;
                            tokenProgress?.Report(tokenCount);
                        }
                    }
                }
            }
        }

        sw.Stop();
        double? tokensPerSecond = null;
        if (tokenCount > 0 && sw.Elapsed.TotalSeconds > 0)
            tokensPerSecond = tokenCount / sw.Elapsed.TotalSeconds;

        return new LlmChatResponse(contentBuilder.ToString(), tokensPerSecond);
    }

    public async Task<LlmEmbedResponse> EmbedAsync(string model, List<string> input, CancellationToken ct)
    {
        var request = new OpenAiEmbedRequest
        {
            Model = model,
            Input = input,
        };

        var resp = await _http.PostAsJsonAsync("v1/embeddings", request, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var dataArray = root.GetProperty("data");
        var embeddings = new double[dataArray.GetArrayLength()][];
        for (var i = 0; i < embeddings.Length; i++)
        {
            var embeddingArray = dataArray[i].GetProperty("embedding");
            embeddings[i] = new double[embeddingArray.GetArrayLength()];
            for (var j = 0; j < embeddings[i].Length; j++)
                embeddings[i][j] = embeddingArray[j].GetDouble();
        }

        return new LlmEmbedResponse(embeddings);
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public List<OpenAiChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("stream")] public bool Stream { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class OpenAiEmbedRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("input")] public List<string> Input { get; set; } = [];
    }
}
