using System.Net;
using System.Text.Json;
using ThinkerAgent.Configuration;
using Microsoft.Extensions.Options;

namespace ThinkerAgent.Services;

public sealed class NotebookApiClient
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<ThinkerOptions> _optionsMonitor;

    public NotebookApiClient(HttpClient http, IOptionsMonitor<ThinkerOptions> optionsMonitor)
    {
        _http = http;
        _optionsMonitor = optionsMonitor;
    }

    private Guid NotebookId => _optionsMonitor.CurrentValue.NotebookId;

    public async Task<PollResult> PullJobAsync(string workerId, string? jobType = null, CancellationToken ct = default)
    {
        var url = $"notebooks/{NotebookId}/jobs/next?worker_id={Uri.EscapeDataString(workerId)}";
        if (jobType is not null)
            url += $"&type={Uri.EscapeDataString(jobType)}";

        var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == HttpStatusCode.NoContent)
            return new PollResult(null, 0);

        if (!resp.IsSuccessStatusCode)
            return new PollResult(null, 0);

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var queueDepth = root.TryGetProperty("queue_depth", out var qd) ? qd.GetInt64() : 0;

        // If "id" is present, this is a real job; otherwise just queue status
        if (root.TryGetProperty("id", out _))
            return new PollResult(root.Clone(), queueDepth);

        return new PollResult(null, queueDepth);
    }

    public record PollResult(JsonElement? Job, long QueueDepth);

    public async Task<bool> CompleteJobAsync(Guid jobId, string workerId, JsonElement result, CancellationToken ct = default)
    {
        var body = JsonSerializer.SerializeToElement(new
        {
            worker_id = workerId,
            result,
        });

        var resp = await _http.PostAsJsonAsync(
            $"notebooks/{NotebookId}/jobs/{jobId}/complete",
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
            $"notebooks/{NotebookId}/jobs/{jobId}/fail",
            body,
            ct);

        return resp.IsSuccessStatusCode;
    }

    public async Task<JsonElement?> GetStatsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"notebooks/{NotebookId}/jobs/stats", ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.Clone();
    }
}
