using System.Net;
using System.Text.Json;
using ThinkerAgent.Configuration;
using Microsoft.Extensions.Options;

namespace ThinkerAgent.Services;

public sealed class NotebookApiClient
{
    private readonly HttpClient _http;
    private readonly ThinkerOptions _options;

    public NotebookApiClient(HttpClient http, IOptions<ThinkerOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<JsonElement?> PullJobAsync(string workerId, string? jobType = null, CancellationToken ct = default)
    {
        var url = $"notebooks/{_options.NotebookId}/jobs/next?worker_id={Uri.EscapeDataString(workerId)}";
        if (jobType is not null)
            url += $"&type={Uri.EscapeDataString(jobType)}";

        var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.NoContent)
            return null;

        if (!resp.IsSuccessStatusCode)
            return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    public async Task<bool> CompleteJobAsync(Guid jobId, string workerId, JsonElement result, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToElement(new
        {
            worker_id = workerId,
            result,
        });

        var resp = await _http.PostAsJsonAsync(
            $"notebooks/{_options.NotebookId}/jobs/{jobId}/complete",
            body,
            ct);

        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> FailJobAsync(Guid jobId, string workerId, string error, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToElement(new
        {
            worker_id = workerId,
            error,
        });

        var resp = await _http.PostAsJsonAsync(
            $"notebooks/{_options.NotebookId}/jobs/{jobId}/fail",
            body,
            ct);

        return resp.IsSuccessStatusCode;
    }

    public async Task<JsonElement?> GetStatsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"notebooks/{_options.NotebookId}/jobs/stats", ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.Clone();
    }
}
