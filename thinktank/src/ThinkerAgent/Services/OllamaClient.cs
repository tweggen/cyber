using System.Text.Json;
using System.Text.Json.Serialization;
using ThinkerAgent.Configuration;

namespace ThinkerAgent.Services;

public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> IsRunningAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync("api/tags", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModel>> ListModelsAsync(CancellationToken ct)
    {
        var resp = await _http.GetAsync("api/tags", ct);
        resp.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var models = new List<OllamaModel>();

        if (doc.RootElement.TryGetProperty("models", out var arr))
        {
            foreach (var m in arr.EnumerateArray())
            {
                models.Add(new OllamaModel(
                    m.GetProperty("name").GetString() ?? "",
                    m.GetProperty("modified_at").GetString() ?? "",
                    m.TryGetProperty("size", out var s) ? s.GetInt64() : 0
                ));
            }
        }

        return models;
    }

    public async Task<OllamaChatResponse> ChatAsync(string model, string prompt, int maxTokens, CancellationToken ct)
    {
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = [new OllamaChatMessage { Role = "user", Content = prompt }],
            Stream = false,
            Options = new OllamaChatOptions { NumPredict = maxTokens },
        };

        var resp = await _http.PostAsJsonAsync("api/chat", request, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var content = root.GetProperty("message").GetProperty("content").GetString() ?? "";

        double? tokensPerSecond = null;
        if (root.TryGetProperty("eval_count", out var evalCount) &&
            root.TryGetProperty("eval_duration", out var evalDuration))
        {
            var count = evalCount.GetDouble();
            var durationNs = evalDuration.GetDouble();
            if (durationNs > 0)
                tokensPerSecond = count / (durationNs / 1_000_000_000.0);
        }

        return new OllamaChatResponse(content, tokensPerSecond);
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public List<OllamaChatMessage> Messages { get; set; } = [];
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("options")] public OllamaChatOptions? Options { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class OllamaChatOptions
    {
        [JsonPropertyName("num_predict")] public int NumPredict { get; set; }
    }
}
