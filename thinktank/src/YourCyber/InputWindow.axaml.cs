using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Cyber.Client.Api;
using Cyber.Client.Filters;
using Cyber.Client.Pipeline;
using YourCyber.ViewModels;

namespace YourCyber;

public partial class InputWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly InputViewModel _viewModel = new();

    public InputWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (_viewModel.IsProcessing)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel.IsProcessing)
            return;

        var files = e.Data.GetFiles();
        if (files == null)
            return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p != null)
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            await ProcessFilesAsync(paths);
    }

    private async void OnPickFilesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.IsProcessing)
            return;

        var storageProvider = GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files to ingest",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported files") { Patterns = ["*.html", "*.htm", "*.txt", "*.md"] },
                new FilePickerFileType("HTML files") { Patterns = ["*.html", "*.htm"] },
                new FilePickerFileType("Text files") { Patterns = ["*.txt", "*.md"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p != null)
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            await ProcessFilesAsync(paths);
    }

    private async Task ProcessFilesAsync(List<string> filePaths)
    {
        _viewModel.IsProcessing = true;
        _viewModel.StatusText = $"Processing {filePaths.Count} file(s)...";
        _viewModel.AddLog(LogLevel.Info, $"Starting ingestion of {filePaths.Count} file(s)");

        try
        {
            var config = await FetchAgentConfigAsync();
            if (config == null)
            {
                _viewModel.AddLog(LogLevel.Error, "Failed to fetch ThinkerAgent config. Is the service running?");
                _viewModel.StatusText = "Error: Could not connect to ThinkerAgent";
                return;
            }

            var filters = new ContentFilterRegistry();
            var http = new HttpClient();
            var batchClient = new NotebookBatchClient(http, new NotebookBatchClientOptions
            {
                ServerUrl = config.ServerUrl,
                NotebookId = config.NotebookId,
                Token = config.Token
            });

            var pipeline = new IngestionPipeline(filters, batchClient);

            var progress = new Progress<IngestionProgress>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var level = p.Stage switch
                    {
                        IngestionStage.Failed => LogLevel.Error,
                        IngestionStage.Skipped => LogLevel.Warning,
                        _ => LogLevel.Info
                    };

                    var message = p.Error ?? p.Message ?? p.Stage.ToString();
                    _viewModel.AddLog(level, message, string.IsNullOrEmpty(p.FileName) ? null : p.FileName);

                    // Auto-scroll log
                    if (LogListBox.ItemCount > 0)
                        LogListBox.ScrollIntoView(LogListBox.ItemCount - 1);
                });
            });

            var result = await Task.Run(() => pipeline.ProcessFilesAsync(filePaths, progress), CancellationToken.None);

            _viewModel.StatusText = $"Done: {result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped";
            _viewModel.AddLog(LogLevel.Info,
                $"Ingestion complete: {result.Succeeded} succeeded, {result.Failed} failed, {result.Skipped} skipped");
        }
        catch (Exception ex)
        {
            _viewModel.AddLog(LogLevel.Error, $"Unexpected error: {ex.Message}");
            _viewModel.StatusText = "Error during ingestion";
        }
        finally
        {
            _viewModel.IsProcessing = false;
        }
    }

    private async Task<AgentConfig?> FetchAgentConfigAsync()
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5948") };
            return await http.GetFromJsonAsync<AgentConfig>("/config", JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private sealed class AgentConfig
    {
        public string ServerUrl { get; set; } = "";
        public string NotebookId { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
