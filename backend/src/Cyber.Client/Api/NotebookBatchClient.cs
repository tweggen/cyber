using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cyber.Client.Api;

public sealed class NotebookBatchClientOptions
{
    public required string ServerUrl { get; init; }
    public required string NotebookId { get; init; }
    public required string Token { get; init; }
}

public sealed class NotebookBatchClient
{
    private const int MaxBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly NotebookBatchClientOptions _options;

    public NotebookBatchClient(HttpClient http, NotebookBatchClientOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<List<BatchWriteResponse>> BatchWriteAsync(
        List<BatchEntryRequest> entries,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (entries.Count == 0)
            return [];

        var responses = new List<BatchWriteResponse>();
        var chunks = Chunk(entries, MaxBatchSize);
        var chunkIndex = 0;
        var totalChunks = (entries.Count + MaxBatchSize - 1) / MaxBatchSize;

        foreach (var chunk in chunks)
        {
            chunkIndex++;
            progress?.Report($"Uploading batch {chunkIndex}/{totalChunks} ({chunk.Count} entries)...");

            var request = new BatchWriteRequest
            {
                Entries = chunk,
                Author = "cyber-input"
            };

            var url = $"{_options.ServerUrl.TrimEnd('/')}/notebooks/{_options.NotebookId}/batch";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

            using var response = await _http.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<BatchWriteResponse>(JsonOptions, ct);
            if (body != null)
                responses.Add(body);
        }

        return responses;
    }

    private static List<List<T>> Chunk<T>(List<T> source, int size)
    {
        var result = new List<List<T>>();
        for (var i = 0; i < source.Count; i += size)
        {
            result.Add(source.GetRange(i, Math.Min(size, source.Count - i)));
        }
        return result;
    }
}
