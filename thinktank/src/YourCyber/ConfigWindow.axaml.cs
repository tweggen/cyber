using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace YourCyber;

public partial class ConfigWindow : Window
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5948") };
    private bool _isSaving;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConfigWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var options = await _http.GetFromJsonAsync<ThinkerOptionsDto>("/config", JsonOptions);

            ServerUrlTextBox.Text = options?.ServerUrl ?? "";
            NotebookIdTextBox.Text = options?.NotebookId.ToString() ?? "";
            TokenTextBox.Text = options?.Token ?? "";
            OllamaUrlTextBox.Text = options?.OllamaUrl ?? "";
            ModelTextBox.Text = options?.Model ?? "";
            WorkerCountUpDown.Value = options?.WorkerCount ?? 1;
            PollIntervalUpDown.Value = (decimal)(options?.PollIntervalSeconds ?? 5.0);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            ServerUrlTextBox.Text = "http://localhost:5000";
            OllamaUrlTextBox.Text = "http://localhost:11434";
            ModelTextBox.Text = "llama3.2";
            WorkerCountUpDown.Value = 1;
            PollIntervalUpDown.Value = 5;
        }
    }

    private void ClearAllErrors()
    {
        ServerUrlErrorText.Text = "";
        ServerUrlErrorText.IsVisible = false;
        NotebookIdErrorText.Text = "";
        NotebookIdErrorText.IsVisible = false;
        TokenErrorText.Text = "";
        TokenErrorText.IsVisible = false;
        OllamaUrlErrorText.Text = "";
        OllamaUrlErrorText.IsVisible = false;
        StatusText.IsVisible = false;
    }

    private void SetError(TextBlock errorTextBlock, string message)
    {
        errorTextBlock.Text = message;
        errorTextBlock.IsVisible = true;
    }

    private void SetStatus(string message, bool isSuccess)
    {
        StatusText.Text = message;
        StatusText.Foreground = isSuccess ? Brushes.Green : Brushes.Orange;
        StatusText.IsVisible = true;
    }

    private bool ValidateInputs()
    {
        ClearAllErrors();
        bool isValid = true;

        var serverUrl = ServerUrlTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            SetError(ServerUrlErrorText, "Server URL is required.");
            isValid = false;
        }
        else if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            SetError(ServerUrlErrorText, "Please enter a valid URL (http:// or https://).");
            isValid = false;
        }

        var notebookId = NotebookIdTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(notebookId))
        {
            SetError(NotebookIdErrorText, "Notebook ID is required.");
            isValid = false;
        }
        else if (!Guid.TryParse(notebookId, out _))
        {
            SetError(NotebookIdErrorText, "Please enter a valid GUID.");
            isValid = false;
        }

        var token = TokenTextBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(token))
        {
            SetError(TokenErrorText, "Token is required.");
            isValid = false;
        }

        var ollamaUrl = OllamaUrlTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(ollamaUrl))
        {
            SetError(OllamaUrlErrorText, "Ollama URL is required.");
            isValid = false;
        }
        else if (!Uri.TryCreate(ollamaUrl, UriKind.Absolute, out _))
        {
            SetError(OllamaUrlErrorText, "Please enter a valid URL.");
            isValid = false;
        }

        return isValid;
    }

    private ThinkerOptionsDto BuildOptionsFromForm()
    {
        return new ThinkerOptionsDto
        {
            ServerUrl = ServerUrlTextBox.Text?.Trim() ?? "",
            NotebookId = Guid.TryParse(NotebookIdTextBox.Text?.Trim(), out var id) ? id : Guid.Empty,
            Token = TokenTextBox.Text ?? "",
            OllamaUrl = OllamaUrlTextBox.Text?.Trim() ?? "",
            Model = ModelTextBox.Text?.Trim() ?? "",
            WorkerCount = (int)(WorkerCountUpDown.Value ?? 1),
            PollIntervalSeconds = (double)(PollIntervalUpDown.Value ?? 5),
        };
    }

    private enum SaveResult
    {
        Success,
        ServerError,
        Timeout,
        ServiceNotRunning
    }

    private async Task<SaveResult> TrySaveConfigAsync(ThinkerOptionsDto options)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));
            var response = await _http.PutAsJsonAsync("/config", options, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                return SaveResult.Success;
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            SetError(ServerUrlErrorText, $"Server error: {errorMessage}");
            return SaveResult.ServerError;
        }
        catch (OperationCanceledException)
        {
            return SaveResult.Timeout;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"ThinkerAgent not reachable: {ex.Message}");
            return SaveResult.ServiceNotRunning;
        }
    }

    private async void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        if (!ValidateInputs()) return;

        var options = BuildOptionsFromForm();
        _isSaving = true;

        try
        {
            var result = await TrySaveConfigAsync(options);

            switch (result)
            {
                case SaveResult.Success:
                    SetStatus("Configuration applied successfully.", isSuccess: true);
                    break;
                case SaveResult.Timeout:
                case SaveResult.ServiceNotRunning:
                    SetError(ServerUrlErrorText, "ThinkerAgent service not reachable.");
                    break;
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        if (!ValidateInputs()) return;

        var options = BuildOptionsFromForm();
        _isSaving = true;

        try
        {
            var result = await TrySaveConfigAsync(options);

            switch (result)
            {
                case SaveResult.Success:
                    Close();
                    break;
                case SaveResult.Timeout:
                case SaveResult.ServiceNotRunning:
                    SetError(ServerUrlErrorText, "ThinkerAgent service not reachable.");
                    break;
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

internal sealed class ThinkerOptionsDto
{
    public string ServerUrl { get; set; } = "";
    public Guid NotebookId { get; set; }
    public string Token { get; set; } = "";
    public int WorkerCount { get; set; } = 1;
    public string OllamaUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public double PollIntervalSeconds { get; set; } = 5.0;
    public List<string>? JobTypes { get; set; }
}
