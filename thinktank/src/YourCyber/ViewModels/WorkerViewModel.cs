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

    public string TokensPerSecondFormatted =>
        TokensPerSecond.HasValue ? $"{TokensPerSecond.Value:F1} tok/s" : "--";

    partial void OnTokensPerSecondChanged(double? value) =>
        OnPropertyChanged(nameof(TokensPerSecondFormatted));
}
