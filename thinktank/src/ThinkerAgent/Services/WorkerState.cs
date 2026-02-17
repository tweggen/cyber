namespace ThinkerAgent.Services;

public enum WorkerStatus
{
    Idle,
    Processing,
    Stopped,
}

public sealed class WorkerInfo
{
    public required string Id { get; init; }
    public WorkerStatus Status { get; set; } = WorkerStatus.Idle;
    public string? CurrentJobType { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsFailed { get; set; }
    public double? TokensPerSecond { get; set; }
    public int TokensGenerated { get; set; }
}

public sealed class WorkerState
{
    private readonly object _lock = new();
    private readonly Dictionary<string, WorkerInfo> _workers = new();
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastNotify = DateTimeOffset.MinValue;

    public event Action? OnStateChanged;

    public bool OllamaConnected { get; set; }
    public bool IsRunning { get; set; }
    public long QueueDepth { get; set; }

    public WorkerInfo GetOrCreateWorker(string id)
    {
        lock (_lock)
        {
            if (!_workers.TryGetValue(id, out var worker))
            {
                worker = new WorkerInfo { Id = id };
                _workers[id] = worker;
            }
            return worker;
        }
    }

    public void RemoveWorker(string id)
    {
        lock (_lock)
        {
            _workers.Remove(id);
        }
        NotifyChanged();
    }

    public WorkerStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var workers = _workers.Values.Select(w => new WorkerInfoSnapshot
            {
                Id = w.Id,
                Status = w.Status.ToString(),
                CurrentJobType = w.CurrentJobType,
                JobsCompleted = w.JobsCompleted,
                JobsFailed = w.JobsFailed,
                TokensPerSecond = w.TokensPerSecond,
                TokensGenerated = w.TokensGenerated,
            }).ToList();

            return new WorkerStateSnapshot
            {
                IsRunning = IsRunning,
                OllamaConnected = OllamaConnected,
                QueueDepth = QueueDepth,
                Workers = workers,
                TotalCompleted = workers.Sum(w => w.JobsCompleted),
                TotalFailed = workers.Sum(w => w.JobsFailed),
                UptimeSeconds = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds,
            };
        }
    }

    public void NotifyChanged() => OnStateChanged?.Invoke();

    public void NotifyThrottled()
    {
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastNotify).TotalMilliseconds < 250)
            return;
        _lastNotify = now;
        OnStateChanged?.Invoke();
    }

    public void ResetUptime() => _startedAt = DateTimeOffset.UtcNow;
}

public sealed class WorkerStateSnapshot
{
    public bool IsRunning { get; init; }
    public bool OllamaConnected { get; init; }
    public long QueueDepth { get; init; }
    public List<WorkerInfoSnapshot> Workers { get; init; } = [];
    public int TotalCompleted { get; init; }
    public int TotalFailed { get; init; }
    public double UptimeSeconds { get; init; }
}

public sealed class WorkerInfoSnapshot
{
    public required string Id { get; init; }
    public required string Status { get; init; }
    public string? CurrentJobType { get; init; }
    public int JobsCompleted { get; init; }
    public int JobsFailed { get; init; }
    public double? TokensPerSecond { get; init; }
    public int TokensGenerated { get; init; }
}
