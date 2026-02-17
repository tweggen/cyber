using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YourCyber.ViewModels;

public partial class AgentStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _ollamaConnected;

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private int _totalCompleted;

    [ObservableProperty]
    private int _totalFailed;

    [ObservableProperty]
    private double _uptimeSeconds;

    public ObservableCollection<WorkerViewModel> Workers { get; } = new();

    public string UptimeFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(UptimeSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }

    public string StatusSummary
    {
        get
        {
            var workerCount = Workers.Count;
            if (!IsRunning || workerCount == 0)
                return "No workers running";
            return $"{workerCount} worker{(workerCount == 1 ? "" : "s")}, {TotalCompleted} jobs done";
        }
    }

    public string OllamaStatusText => OllamaConnected ? "Connected" : "Disconnected";

    public bool IsEmpty => !IsRunning && Workers.Count == 0;

    partial void OnUptimeSecondsChanged(double value) =>
        OnPropertyChanged(nameof(UptimeFormatted));

    partial void OnOllamaConnectedChanged(bool value) =>
        OnPropertyChanged(nameof(OllamaStatusText));

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnTotalCompletedChanged(int value) =>
        OnPropertyChanged(nameof(StatusSummary));

    public void UpdateFromSnapshot(WorkerStateSnapshot snapshot)
    {
        IsRunning = snapshot.IsRunning;
        OllamaConnected = snapshot.OllamaConnected;
        TotalCompleted = snapshot.TotalCompleted;
        TotalFailed = snapshot.TotalFailed;
        UptimeSeconds = snapshot.UptimeSeconds;

        var seenIds = new HashSet<string>();

        foreach (var ws in snapshot.Workers)
        {
            seenIds.Add(ws.Id);
            var existing = Workers.FirstOrDefault(w => w.Id == ws.Id);
            if (existing == null)
            {
                existing = new WorkerViewModel { Id = ws.Id };
                Workers.Add(existing);
            }

            existing.Status = ws.Status;
            existing.CurrentJobType = ws.CurrentJobType;
            existing.JobsCompleted = ws.JobsCompleted;
            existing.JobsFailed = ws.JobsFailed;
            existing.TokensPerSecond = ws.TokensPerSecond;
            existing.TokensGenerated = ws.TokensGenerated;
        }

        for (int i = Workers.Count - 1; i >= 0; i--)
        {
            if (!seenIds.Contains(Workers[i].Id))
                Workers.RemoveAt(i);
        }

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed class WorkerStateSnapshot
{
    public bool IsRunning { get; set; }
    public bool OllamaConnected { get; set; }
    public List<WorkerInfoSnapshot> Workers { get; set; } = [];
    public int TotalCompleted { get; set; }
    public int TotalFailed { get; set; }
    public double UptimeSeconds { get; set; }
}

public sealed class WorkerInfoSnapshot
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string? CurrentJobType { get; set; }
    public int JobsCompleted { get; set; }
    public int JobsFailed { get; set; }
    public double? TokensPerSecond { get; set; }
    public int TokensGenerated { get; set; }
}
