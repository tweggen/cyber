using CommunityToolkit.Mvvm.ComponentModel;

namespace YourCyber.ViewModels;

public partial class WorkerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _status = "Idle";

    [ObservableProperty]
    private string? _currentJobType;

    [ObservableProperty]
    private int _jobsCompleted;

    [ObservableProperty]
    private int _jobsFailed;

    [ObservableProperty]
    private double? _tokensPerSecond;

    [ObservableProperty]
    private int _tokensGenerated;

    public string TokensPerSecondFormatted =>
        TokensPerSecond.HasValue ? $"{TokensPerSecond.Value:F1} tok/s" : "--";

    public string ProgressText =>
        Status == "Processing" && TokensGenerated > 0
            ? $"{TokensGenerated} tok"
            : "";

    partial void OnTokensPerSecondChanged(double? value) =>
        OnPropertyChanged(nameof(TokensPerSecondFormatted));

    partial void OnTokensGeneratedChanged(int value) =>
        OnPropertyChanged(nameof(ProgressText));

    partial void OnStatusChanged(string value) =>
        OnPropertyChanged(nameof(ProgressText));
}
