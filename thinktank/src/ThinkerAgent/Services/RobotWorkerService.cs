using System.Text.Json;
using Microsoft.Extensions.Options;
using ThinkerAgent.Configuration;
using ThinkerAgent.Prompts;

namespace ThinkerAgent.Services;

public sealed class RobotWorkerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<ThinkerOptions> _optionsMonitor;
    private readonly WorkerState _state;
    private readonly ILogger<RobotWorkerService> _logger;

    public RobotWorkerService(
        IServiceProvider services,
        IOptionsMonitor<ThinkerOptions> optionsMonitor,
        WorkerState state,
        ILogger<RobotWorkerService> logger)
    {
        _services = services;
        _optionsMonitor = optionsMonitor;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            using var restartCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            using var changeRegistration = _optionsMonitor.OnChange(_ =>
            {
                _logger.LogInformation("Configuration changed, restarting workers...");
                try { restartCts.Cancel(); } catch (ObjectDisposedException) { }
            });

            _state.IsRunning = true;
            _state.ResetUptime();
            _state.NotifyChanged();

            _logger.LogInformation(
                "Starting {Count} worker(s), model={Model}, server={Server}",
                options.WorkerCount, options.Model, options.ServerUrl);

            var tasks = new List<Task>();
            for (var i = 0; i < options.WorkerCount; i++)
            {
                var workerId = $"thinker-{Environment.MachineName}-{i}";
                tasks.Add(RunWorkerLoop(workerId, options, restartCts.Token));
            }

            await Task.WhenAll(tasks);

            if (stoppingToken.IsCancellationRequested)
                break;

            _logger.LogInformation("Workers restarting with new configuration...");

            // Brief delay to debounce rapid file-system events.
            try { await Task.Delay(500, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _state.IsRunning = false;
        _state.NotifyChanged();
    }

    private int _jobTypeIndex;

    private async Task RunWorkerLoop(string workerId, ThinkerOptions options, CancellationToken ct)
    {
        var worker = _state.GetOrCreateWorker(workerId);
        var pollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds);
        var consecutiveEmpty = 0;

        _logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var apiClient = scope.ServiceProvider.GetRequiredService<NotebookApiClient>();
                var llmClient = scope.ServiceProvider.GetRequiredService<ILlmClient>();

                // Check LLM BEFORE claiming a job — don't burn retry budgets while LLM is down
                var llmOk = await llmClient.IsRunningAsync(ct);
                _state.LlmConnected = llmOk;
                if (!llmOk)
                {
                    worker.Status = WorkerStatus.Idle;
                    worker.CurrentJobType = null;
                    if (consecutiveEmpty % 12 == 0)
                        _logger.LogWarning("Worker {WorkerId}: LLM not reachable, waiting...", workerId);
                    consecutiveEmpty++;
                    _state.NotifyChanged();
                    await Task.Delay(pollInterval, ct);
                    continue;
                }

                // Pick a job type if filtered; server-side priority handles ordering
                string? jobType = null;
                if (options.JobTypes is { Count: > 0 })
                    jobType = options.JobTypes[_jobTypeIndex++ % options.JobTypes.Count];

                var pollResult = await apiClient.PullJobAsync(workerId, jobType, ct);
                _state.QueueDepth = pollResult.QueueDepth;

                if (pollResult.Job is null)
                {
                    worker.Status = WorkerStatus.Idle;
                    worker.CurrentJobType = null;
                    consecutiveEmpty++;
                    if (consecutiveEmpty % 12 == 1)
                        _logger.LogDebug("Worker {WorkerId}: no jobs available (queue: {QueueDepth}), waiting...", workerId, pollResult.QueueDepth);

                    _state.NotifyChanged();
                    await Task.Delay(pollInterval, ct);
                    continue;
                }

                consecutiveEmpty = 0;
                var jobElement = pollResult.Job.Value;
                var jobId = jobElement.GetProperty("id").GetGuid();
                var jobTypeStr = jobElement.GetProperty("job_type").GetString()!;
                var payload = jobElement.GetProperty("payload");

                worker.Status = WorkerStatus.Processing;
                worker.CurrentJobType = jobTypeStr;
                _state.NotifyChanged();

                _logger.LogInformation("Worker {WorkerId}: processing job {JobId} (type={JobType})", workerId, jobId, jobTypeStr);

                try
                {

                    // EMBED_CLAIMS: call embedding API instead of chat
                    if (jobTypeStr == "EMBED_CLAIMS")
                    {
                        var claimTexts = payload.GetProperty("claim_texts")
                            .EnumerateArray()
                            .Select(e => e.GetString()!)
                            .ToList();

                        var embedResponse = await llmClient.EmbedAsync(
                            options.EmbeddingModel, claimTexts, ct);

                        // Average per-claim embeddings into single vector
                        var dim = embedResponse.Embeddings[0].Length;
                        var avg = new double[dim];
                        foreach (var vec in embedResponse.Embeddings)
                            for (var d = 0; d < dim; d++)
                                avg[d] += vec[d];
                        for (var d = 0; d < dim; d++)
                            avg[d] /= embedResponse.Embeddings.Length;

                        // Normalize to unit length
                        var norm = Math.Sqrt(avg.Sum(v => v * v));
                        if (norm > 0)
                            for (var d = 0; d < dim; d++)
                                avg[d] /= norm;

                        var resultElement = JsonSerializer.SerializeToElement(new { embedding = avg });
                        if (await apiClient.CompleteJobAsync(jobId, workerId, resultElement, ct))
                        {
                            worker.JobsCompleted++;
                            _logger.LogInformation(
                                "Worker {WorkerId}: embed job {JobId} completed (dim={Dim})",
                                workerId, jobId, dim);
                        }
                        else
                        {
                            worker.JobsFailed++;
                            _logger.LogError("Worker {WorkerId}: failed to submit embed result for job {JobId}", workerId, jobId);
                        }

                        _state.NotifyChanged();
                        continue;
                    }

                    // Build prompt
                    var prompt = PromptBuilder.BuildPrompt(jobTypeStr, payload);

                    // Call LLM with streaming progress
                    worker.TokensGenerated = 0;
                    var progress = new Progress<int>(count =>
                    {
                        worker.TokensGenerated = count;
                        _state.NotifyThrottled();
                    });
                    var llmResponse = await llmClient.ChatAsync(options.Model, prompt, 2048, progress, ct);
                    worker.TokensGenerated = 0;
                    worker.TokensPerSecond = llmResponse.TokensPerSecond;

                    // Parse result
                    var result = ResultParser.ParseResult(jobTypeStr, llmResponse.Content, payload);

                    // Complete job
                    var resultElement2 = JsonSerializer.SerializeToElement(result);
                    if (await apiClient.CompleteJobAsync(jobId, workerId, resultElement2, ct))
                    {
                        worker.JobsCompleted++;
                        _logger.LogInformation(
                            "Worker {WorkerId}: job {JobId} completed (total: {Completed} completed, {Failed} failed)",
                            workerId, jobId, worker.JobsCompleted, worker.JobsFailed);
                    }
                    else
                    {
                        worker.JobsFailed++;
                        _logger.LogError("Worker {WorkerId}: failed to submit result for job {JobId}", workerId, jobId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker {WorkerId}: job {JobId} failed", workerId, jobId);
                    await apiClient.FailJobAsync(jobId, workerId, ex.Message, ct);
                    worker.JobsFailed++;
                }

                _state.NotifyChanged();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId}: unexpected error", workerId);
                await Task.Delay(pollInterval, ct);
            }
        }

        worker.Status = WorkerStatus.Stopped;
        _state.NotifyChanged();
        _logger.LogInformation(
            "Worker {WorkerId} stopped. Completed: {Completed}, Failed: {Failed}",
            workerId, worker.JobsCompleted, worker.JobsFailed);
    }
}
