using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notebook.Server.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;

    public EmbeddingService(IConfiguration config)
    {
        var baseUrl = config["Embedding:OllamaUrl"] ?? "http://localhost:11434";
        _model = config["Embedding:Model"] ?? "nomic-embed-text";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<double[]> EmbedQueryAsync(string text, CancellationToken ct)
    {
        var request = new OllamaEmbedRequest { Model = _model, Input = [text] };
        var response = await _http.PostAsJsonAsync("/api/embed", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        if (result?.Embeddings is null || result.Embeddings.Count == 0)
            throw new InvalidOperationException("Ollama returned no embeddings");

        return result.Embeddings[0];
    }

    private sealed record OllamaEmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required List<string> Input { get; init; }
    }

    private sealed record OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<double[]>? Embeddings { get; init; }
    }
}
