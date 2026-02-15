using System.Text.Json;
using Microsoft.Extensions.Options;
using ThinkerAgent.Configuration;
using ThinkerAgent.Prompts;

namespace ThinkerAgent.Services;

public sealed class RobotWorkerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ThinkerOptions _options;
    private readonly WorkerState _state;
    private readonly ILogger<RobotWorkerService> _logger;

    public RobotWorkerService(
        IServiceProvider services,
        IOptions<ThinkerOptions> options,
        WorkerState state,
        ILogger<RobotWorkerService> logger)
    {
        _services = services;
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _state.IsRunning = true;
        _state.ResetUptime();
        _state.NotifyChanged();

        _logger.LogInformation(
            "Starting {Count} worker(s), model={Model}, server={Server}",
            _options.WorkerCount, _options.Model, _options.ServerUrl);

        var tasks = new List<Task>();
        for (var i = 0; i < _options.WorkerCount; i++)
        {
            var workerId = $"thinker-{Environment.MachineName}-{i}";
            tasks.Add(RunWorkerLoop(workerId, stoppingToken));
        }

        await Task.WhenAll(tasks);

        _state.IsRunning = false;
        _state.NotifyChanged();
    }

    private async Task RunWorkerLoop(string workerId, CancellationToken ct)
    {
        var worker = _state.GetOrCreateWorker(workerId);
        var pollInterval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
        var consecutiveEmpty = 0;

        _logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var apiClient = scope.ServiceProvider.GetRequiredService<NotebookApiClient>();
                var ollamaClient = scope.ServiceProvider.GetRequiredService<IOllamaClient>();

                // Pick a job type if filtered
                string? jobType = null;
                if (_options.JobTypes is { Count: > 0 })
                    jobType = _options.JobTypes[consecutiveEmpty % _options.JobTypes.Count];

                var job = await apiClient.PullJobAsync(workerId, jobType, ct);

                if (job is null)
                {
                    worker.Status = WorkerStatus.Idle;
                    worker.CurrentJobType = null;
                    consecutiveEmpty++;
                    if (consecutiveEmpty % 12 == 1)
                        _logger.LogDebug("Worker {WorkerId}: no jobs available, waiting...", workerId);

                    _state.NotifyChanged();
                    await Task.Delay(pollInterval, ct);
                    continue;
                }

                consecutiveEmpty = 0;
                var jobElement = job.Value;
                var jobId = jobElement.GetProperty("id").GetGuid();
                var jobTypeStr = jobElement.GetProperty("job_type").GetString()!;
                var payload = jobElement.GetProperty("payload");

                worker.Status = WorkerStatus.Processing;
                worker.CurrentJobType = jobTypeStr;
                _state.NotifyChanged();

                _logger.LogInformation("Worker {WorkerId}: processing job {JobId} (type={JobType})", workerId, jobId, jobTypeStr);

                try
                {
                    // Build prompt
                    var prompt = PromptBuilder.BuildPrompt(jobTypeStr, payload);

                    // Check ollama
                    var ollamaOk = await ollamaClient.IsRunningAsync(ct);
                    _state.OllamaConnected = ollamaOk;
                    if (!ollamaOk)
                    {
                        await apiClient.FailJobAsync(jobId, workerId, "Ollama is not running", ct);
                        worker.JobsFailed++;
                        _state.NotifyChanged();
                        await Task.Delay(pollInterval, ct);
                        continue;
                    }

                    // Call LLM
                    var llmResponse = await ollamaClient.ChatAsync(_options.Model, prompt, 2048, ct);
                    worker.TokensPerSecond = llmResponse.TokensPerSecond;

                    // Parse result
                    var result = ResultParser.ParseResult(jobTypeStr, llmResponse.Content, payload);

                    // Complete job
                    var resultElement = JsonSerializer.SerializeToElement(result);
                    if (await apiClient.CompleteJobAsync(jobId, workerId, resultElement, ct))
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
