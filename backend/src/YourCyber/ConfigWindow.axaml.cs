using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using YourCyber.Models;
using YourCyber.Services;

namespace YourCyber;

public partial class ConfigWindow : Window
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5948") };
    private bool _isSaving;
    private ProfileStore _profileStore = new();
    private bool _suppressProfileChange;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConfigWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Loading ──────────────────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _profileStore = ProfileService.Load();

        ThinkerOptionsDto? agentConfig = null;
        try
        {
            agentConfig = await _http.GetFromJsonAsync<ThinkerOptionsDto>("/config", JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
        }

        // Seed a Default profile on first launch
        if (_profileStore.Profiles.Count == 0)
        {
            _profileStore.Profiles.Add(new ServerProfile
            {
                Name = "Default",
                ServerUrl = agentConfig?.ServerUrl ?? "http://localhost:5000",
                NotebookId = agentConfig?.NotebookId ?? Guid.Empty,
                Token = agentConfig?.Token ?? ""
            });
            _profileStore.ActiveProfileName = "Default";
        }

        PopulateProfileComboBox();
        SelectActiveProfile();
        LoadProfileIntoForm(GetActiveProfile());
        LoadGlobalFields(agentConfig);
    }

    private void LoadGlobalFields(ThinkerOptionsDto? options)
    {
        SelectApiType(options?.ApiType ?? "Ollama");
        LlmUrlTextBox.Text = options?.LlmUrl ?? "http://localhost:11434";
        ApiKeyTextBox.Text = options?.ApiKey ?? "";
        ModelTextBox.Text = options?.Model ?? "llama3.2";
        EmbeddingModelTextBox.Text = options?.EmbeddingModel ?? "nomic-embed-text";
        WorkerCountUpDown.Value = options?.WorkerCount ?? 1;
        PollIntervalUpDown.Value = (decimal)(options?.PollIntervalSeconds ?? 5.0);
    }

    private void LoadProfileIntoForm(ServerProfile? profile)
    {
        ServerUrlTextBox.Text = profile?.ServerUrl ?? "";
        NotebookIdTextBox.Text = profile?.NotebookId.ToString() ?? "";
        TokenTextBox.Text = profile?.Token ?? "";
    }

    private void SaveFormIntoProfile(ServerProfile? profile)
    {
        if (profile is null) return;
        profile.ServerUrl = ServerUrlTextBox.Text?.Trim() ?? "";
        profile.NotebookId = Guid.TryParse(NotebookIdTextBox.Text?.Trim(), out var id) ? id : Guid.Empty;
        profile.Token = TokenTextBox.Text ?? "";
    }

    // ── Profile ComboBox ─────────────────────────────────────────────

    private void PopulateProfileComboBox()
    {
        _suppressProfileChange = true;
        ProfileComboBox.Items.Clear();
        foreach (var p in _profileStore.Profiles)
            ProfileComboBox.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Name });
        _suppressProfileChange = false;

        ProfileComboBox.SelectionChanged += OnProfileSelectionChanged;
    }

    private void SelectActiveProfile()
    {
        _suppressProfileChange = true;
        for (var i = 0; i < ProfileComboBox.Items.Count; i++)
        {
            if (ProfileComboBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == _profileStore.ActiveProfileName)
            {
                ProfileComboBox.SelectedIndex = i;
                _suppressProfileChange = false;
                return;
            }
        }
        if (ProfileComboBox.Items.Count > 0)
            ProfileComboBox.SelectedIndex = 0;
        _suppressProfileChange = false;
    }

    private ServerProfile? GetActiveProfile()
    {
        var name = GetSelectedProfileName();
        return _profileStore.Profiles.Find(p => p.Name == name);
    }

    private string GetSelectedProfileName()
    {
        if (ProfileComboBox.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString() ?? "";
        return _profileStore.ActiveProfileName;
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressProfileChange) return;

        // Save current form values into the previously active profile
        var prev = _profileStore.Profiles.Find(p => p.Name == _profileStore.ActiveProfileName);
        SaveFormIntoProfile(prev);

        // Switch to the new profile
        _profileStore.ActiveProfileName = GetSelectedProfileName();
        LoadProfileIntoForm(GetActiveProfile());
    }

    // ── Profile management buttons ───────────────────────────────────

    private async void OnAddProfileClicked(object? sender, RoutedEventArgs e)
    {
        var name = await PromptForNameAsync("New Profile", "Enter a name for the new profile:");
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_profileStore.Profiles.Exists(p => p.Name == name))
        {
            SetStatus($"A profile named \"{name}\" already exists.", isSuccess: false);
            return;
        }

        // Save current form into current profile before switching
        SaveFormIntoProfile(GetActiveProfile());

        _profileStore.Profiles.Add(new ServerProfile { Name = name });
        _profileStore.ActiveProfileName = name;

        // Re-hook after repopulate
        ProfileComboBox.SelectionChanged -= OnProfileSelectionChanged;
        PopulateProfileComboBox();
        SelectActiveProfile();
        LoadProfileIntoForm(GetActiveProfile());
    }

    private void OnDeleteProfileClicked(object? sender, RoutedEventArgs e)
    {
        if (_profileStore.Profiles.Count <= 1)
        {
            SetStatus("Cannot delete the last profile.", isSuccess: false);
            return;
        }

        var current = GetActiveProfile();
        if (current is null) return;

        _profileStore.Profiles.Remove(current);
        _profileStore.ActiveProfileName = _profileStore.Profiles[0].Name;

        ProfileComboBox.SelectionChanged -= OnProfileSelectionChanged;
        PopulateProfileComboBox();
        SelectActiveProfile();
        LoadProfileIntoForm(GetActiveProfile());
    }

    private async void OnRenameProfileClicked(object? sender, RoutedEventArgs e)
    {
        var current = GetActiveProfile();
        if (current is null) return;

        var newName = await PromptForNameAsync("Rename Profile", "Enter a new name:", current.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == current.Name) return;

        if (_profileStore.Profiles.Exists(p => p.Name == newName))
        {
            SetStatus($"A profile named \"{newName}\" already exists.", isSuccess: false);
            return;
        }

        current.Name = newName;
        _profileStore.ActiveProfileName = newName;

        ProfileComboBox.SelectionChanged -= OnProfileSelectionChanged;
        PopulateProfileComboBox();
        SelectActiveProfile();
    }

    private async Task<string?> PromptForNameAsync(string title, string message, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 320,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var textBox = new TextBox { Text = defaultValue, Margin = new Avalonia.Thickness(10, 0, 10, 10) };
        string? result = null;

        var okButton = new Button { Content = "OK", Width = 70, Margin = new Avalonia.Thickness(5) };
        var cancelButton = new Button { Content = "Cancel", Width = 70, Margin = new Avalonia.Thickness(5) };

        okButton.Click += (_, _) => { result = textBox.Text?.Trim(); dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { okButton, cancelButton }
        };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(10),
            Children =
            {
                new TextBlock { Text = message, Margin = new Avalonia.Thickness(10, 10, 10, 5) },
                textBox,
                buttons
            }
        };

        await dialog.ShowDialog(this);
        return result;
    }

    // ── API Type helpers ─────────────────────────────────────────────

    private void SelectApiType(string apiType)
    {
        for (var i = 0; i < ApiTypeComboBox.Items.Count; i++)
        {
            if (ApiTypeComboBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == apiType)
            {
                ApiTypeComboBox.SelectedIndex = i;
                return;
            }
        }
        ApiTypeComboBox.SelectedIndex = 0;
    }

    private string GetSelectedApiType()
    {
        if (ApiTypeComboBox.SelectedItem is ComboBoxItem item)
            return item.Tag?.ToString() ?? "Ollama";
        return "Ollama";
    }

    // ── Validation ───────────────────────────────────────────────────

    private void ClearAllErrors()
    {
        ServerUrlErrorText.Text = "";
        ServerUrlErrorText.IsVisible = false;
        NotebookIdErrorText.Text = "";
        NotebookIdErrorText.IsVisible = false;
        TokenErrorText.Text = "";
        TokenErrorText.IsVisible = false;
        LlmUrlErrorText.Text = "";
        LlmUrlErrorText.IsVisible = false;
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

        var llmUrl = LlmUrlTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(llmUrl))
        {
            SetError(LlmUrlErrorText, "LLM URL is required.");
            isValid = false;
        }
        else if (!Uri.TryCreate(llmUrl, UriKind.Absolute, out _))
        {
            SetError(LlmUrlErrorText, "Please enter a valid URL.");
            isValid = false;
        }

        return isValid;
    }

    // ── Build DTO ────────────────────────────────────────────────────

    private ThinkerOptionsDto BuildOptionsFromForm()
    {
        return new ThinkerOptionsDto
        {
            ServerUrl = ServerUrlTextBox.Text?.Trim() ?? "",
            NotebookId = Guid.TryParse(NotebookIdTextBox.Text?.Trim(), out var id) ? id : Guid.Empty,
            Token = TokenTextBox.Text ?? "",
            ApiType = GetSelectedApiType(),
            LlmUrl = LlmUrlTextBox.Text?.Trim() ?? "",
            ApiKey = ApiKeyTextBox.Text ?? "",
            Model = ModelTextBox.Text?.Trim() ?? "",
            EmbeddingModel = EmbeddingModelTextBox.Text?.Trim() ?? "",
            WorkerCount = (int)(WorkerCountUpDown.Value ?? 1),
            PollIntervalSeconds = (double)(PollIntervalUpDown.Value ?? 5),
        };
    }

    // ── Save to ThinkerAgent ─────────────────────────────────────────

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

    private void PersistProfiles()
    {
        SaveFormIntoProfile(GetActiveProfile());
        _profileStore.ActiveProfileName = GetSelectedProfileName();
        ProfileService.Save(_profileStore);
    }

    // ── Button handlers ──────────────────────────────────────────────

    private async void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (_isSaving) return;
        if (!ValidateInputs()) return;

        var options = BuildOptionsFromForm();
        _isSaving = true;

        try
        {
            PersistProfiles();
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
            PersistProfiles();
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
    public string ApiType { get; set; } = "Ollama";
    public string LlmUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public double PollIntervalSeconds { get; set; } = 5.0;
    public List<string>? JobTypes { get; set; }
}
