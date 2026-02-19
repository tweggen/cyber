using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notebook.Server.Configuration;

namespace Notebook.Server.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly EmbeddingOptions _options;

    public EmbeddingService(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_options.Url),
            Timeout = TimeSpan.FromSeconds(30),
        };

        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.Token);
        }
    }

    public async Task<double[]> EmbedQueryAsync(string text, CancellationToken ct)
    {
        return _options.ApiType switch
        {
            EmbeddingApiType.Ollama => await EmbedWithOllamaAsync(text, ct),
            EmbeddingApiType.OpenAI => await EmbedWithOpenAIAsync(text, ct),
            _ => throw new InvalidOperationException($"Unsupported API type: {_options.ApiType}"),
        };
    }

    private async Task<double[]> EmbedWithOllamaAsync(string text, CancellationToken ct)
    {
        var request = new OllamaEmbedRequest
        {
            Model = _options.Model,
            Input = [text],
        };

        var response = await _http.PostAsJsonAsync("/api/embed", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        if (result?.Embeddings is null || result.Embeddings.Count == 0)
            throw new InvalidOperationException("Ollama returned no embeddings");

        return result.Embeddings[0];
    }

    private async Task<double[]> EmbedWithOpenAIAsync(string text, CancellationToken ct)
    {
        var request = new OpenAIEmbedRequest
        {
            Model = _options.Model,
            Input = text,
        };

        var response = await _http.PostAsJsonAsync("/embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbedResponse>(ct);
        if (result?.Data is null || result.Data.Count == 0)
            throw new InvalidOperationException("OpenAI returned no embeddings");

        return result.Data[0].Embedding;
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

    private sealed record OpenAIEmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private sealed record OpenAIEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIEmbedData>? Data { get; init; }
    }

    private sealed record OpenAIEmbedData
    {
        [JsonPropertyName("embedding")]
        public required double[] Embedding { get; init; }
    }
}
