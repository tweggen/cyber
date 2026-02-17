using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YourCyber.ViewModels;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public sealed record LogEntry
{
    public required DateTime Timestamp { get; init; }
    public required LogLevel Level { get; init; }
    public required string Message { get; init; }
    public string? FileName { get; init; }
}

public partial class InputViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusText = "Drop files here or use Pick Files...";

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public void AddLog(LogLevel level, string message, string? fileName = null)
    {
        LogEntries.Add(new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            FileName = fileName
        });
    }

    public void Clear()
    {
        LogEntries.Clear();
        StatusText = "Drop files here or use Pick Files...";
    }
}
