using System.Net.Http.Json;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using YourCyber.Platform;
using YourCyber.ViewModels;

namespace YourCyber;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private NativeMenu? _trayMenu;
    private NativeMenuItem? _statusItem;
    private NativeMenuItem? _startStopItem;
    private NativeMenuItem? _quitLaunchItem;

    private readonly IServiceLauncher _serviceLauncher = ServiceLauncherFactory.Create();
    private bool _serviceAvailable = true;

    private ConfigWindow? _configWindow;
    private StatusWindow? _statusWindow;
    private InputWindow? _inputWindow;

    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5948") };
    private HubConnection? _hubConnection;
    private DispatcherTimer? _pollTimer;

    private readonly AgentStatusViewModel _agentStatus = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            SetupTrayIcon();

            _ = SetupSignalRConnectionAsync();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _pollTimer.Tick += async (s, e) =>
            {
                if (_hubConnection?.State != HubConnectionState.Connected)
                {
                    await UpdateServiceStateAsync();
                }
            };
            _pollTimer.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        _statusItem = new NativeMenuItem("Connecting...")
        {
            IsEnabled = false
        };

        _startStopItem = new NativeMenuItem("Connecting...")
        {
            IsEnabled = false
        };
        _startStopItem.Click += async (s, e) =>
        {
            if (_startStopItem.Header?.ToString()?.StartsWith("Start") == true)
            {
                await _http.PostAsync("/start", null);
            }
            else
            {
                await _http.PostAsync("/stop", null);
            }
        };

        _quitLaunchItem = new NativeMenuItem("Quit Service");
        _quitLaunchItem.Click += OnQuitLaunchClicked;

        var configItem = new NativeMenuItem("Configure...");
        configItem.Click += (s, e) => ShowConfigWindow();

        var workersItem = new NativeMenuItem("Workers...");
        workersItem.Click += (s, e) => ShowStatusWindow();

        var inputItem = new NativeMenuItem("Input...");
        inputItem.Click += (s, e) => ShowInputWindow();

        var exitItem = new NativeMenuItem("Exit YourCyber");
        exitItem.Click += (s, e) =>
        {
            _trayIcon?.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };

        _trayMenu = new NativeMenu
        {
            _statusItem,
            _startStopItem,
            new NativeMenuItemSeparator(),
            _quitLaunchItem,
            new NativeMenuItemSeparator(),
            configItem,
            workersItem,
            inputItem,
            new NativeMenuItemSeparator(),
            exitItem
        };

        _trayIcon = new TrayIcon
        {
            ToolTipText = "YourCyber",
            Menu = _trayMenu,
            IsVisible = true,
            Icon = CreateTrayIcon()
        };

        _trayIcon.Clicked += (s, e) => ShowStatusWindow();
    }

    private WindowIcon? CreateTrayIcon()
    {
        try
        {
            var uri = new Uri("avares://YourCyber/Assets/cyber-tray.png");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                return new WindowIcon(stream);
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "cyber-tray.png");
            if (File.Exists(iconPath))
            {
                return new WindowIcon(iconPath);
            }

            return CreateFallbackIcon();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
            return CreateFallbackIcon();
        }
    }

    private WindowIcon? CreateFallbackIcon()
    {
        try
        {
            var bitmap = new WriteableBitmap(
                new Avalonia.PixelSize(32, 32),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            using (var fb = bitmap.Lock())
            {
                unsafe
                {
                    var ptr = (uint*)fb.Address;
                    // Teal/cyan color for YourCyber: #00B4D8
                    uint color = 0xFFD8B400; // BGRA
                    uint border = 0xFF8A7600;
                    uint mid = 0xFFB09300;

                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 32; x++)
                        {
                            bool isBorder = x < 2 || x >= 30 || y < 2 || y >= 30;
                            bool isInner = x >= 4 && x < 28 && y >= 4 && y < 28;

                            if (isBorder)
                                ptr[y * 32 + x] = border;
                            else if (isInner)
                                ptr[y * 32 + x] = color;
                            else
                                ptr[y * 32 + x] = mid;
                        }
                    }
                }
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            return new WindowIcon(ms);
        }
        catch
        {
            return null;
        }
    }

    private void ShowConfigWindow()
    {
        if (_configWindow == null || !_configWindow.IsVisible)
        {
            _configWindow = new ConfigWindow();
            _configWindow.Closed += (s, e) => _configWindow = null;
            _configWindow.Show();
        }
        else
        {
            _configWindow.Activate();
        }
    }

    private void ShowStatusWindow()
    {
        if (_statusWindow == null || !_statusWindow.IsVisible)
        {
            _statusWindow = new StatusWindow(_agentStatus);
            _statusWindow.Closed += (s, e) => _statusWindow = null;
            _statusWindow.Show();
        }
        else
        {
            _statusWindow.Activate();
        }
    }

    private void ShowInputWindow()
    {
        if (_inputWindow == null || !_inputWindow.IsVisible)
        {
            _inputWindow = new InputWindow();
            _inputWindow.Closed += (s, e) => _inputWindow = null;
            _inputWindow.Show();
        }
        else
        {
            _inputWindow.Activate();
        }
    }

    private async void OnQuitLaunchClicked(object? sender, EventArgs e)
    {
        if (_serviceAvailable)
        {
            try
            {
                await _http.PostAsync("/quit", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to quit service: {ex.Message}");
            }
        }
        else if (_serviceLauncher.IsSupported)
        {
            if (_quitLaunchItem != null)
            {
                _quitLaunchItem.Header = "Launching...";
                _quitLaunchItem.IsEnabled = false;
            }

            var success = await _serviceLauncher.TryLaunchAsync();

            if (!success)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_quitLaunchItem != null)
                    {
                        _quitLaunchItem.Header = "Launch Service";
                        _quitLaunchItem.IsEnabled = true;
                    }
                });
            }
        }
    }

    private async Task SetupSignalRConnectionAsync()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5948/thinkercontrolhub")
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            _hubConnection.On<WorkerStateSnapshot>("WorkerStateChanged", snapshot =>
            {
                Dispatcher.UIThread.Post(() => UpdateUIWithSnapshot(snapshot));
            });

            _hubConnection.Reconnecting += error =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_statusItem != null) _statusItem.Header = "Reconnecting...";
                    if (_startStopItem != null) _startStopItem.IsEnabled = false;
                });
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_statusItem != null) _statusItem.Header = "Reconnected";
                });
                return Task.CompletedTask;
            };

            _hubConnection.Closed += async error =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_statusItem != null) _statusItem.Header = "Disconnected";
                    if (_startStopItem != null) _startStopItem.IsEnabled = false;
                });

                await Task.Delay(5000);
                await ConnectSignalRAsync();
            };

            await ConnectSignalRAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignalR setup error: {ex}");
        }
    }

    private async Task ConnectSignalRAsync()
    {
        try
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StartAsync();
                await _hubConnection.InvokeAsync("RequestCurrentState");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SignalR connection error: {ex}");
        }
    }

    private async Task UpdateServiceStateAsync()
    {
        try
        {
            var snapshot = await _http.GetFromJsonAsync<WorkerStateSnapshot>("/status", JsonOptions);

            if (snapshot != null)
            {
                Dispatcher.UIThread.Post(() => UpdateUIWithSnapshot(snapshot));
            }
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                _serviceAvailable = false;
                if (_statusItem != null) _statusItem.Header = "Service unavailable";
                if (_startStopItem != null)
                {
                    _startStopItem.Header = "Service unavailable";
                    _startStopItem.IsEnabled = false;
                }
                if (_quitLaunchItem != null)
                {
                    if (_serviceLauncher.IsSupported)
                    {
                        _quitLaunchItem.Header = "Launch Service";
                        _quitLaunchItem.IsEnabled = true;
                    }
                    else
                    {
                        _quitLaunchItem.Header = "Service unavailable";
                        _quitLaunchItem.IsEnabled = false;
                    }
                }
            });
        }
    }

    private void UpdateUIWithSnapshot(WorkerStateSnapshot snapshot)
    {
        _serviceAvailable = true;

        _agentStatus.UpdateFromSnapshot(snapshot);

        if (_statusItem != null)
        {
            _statusItem.Header = _agentStatus.StatusSummary;
        }

        if (_quitLaunchItem != null)
        {
            _quitLaunchItem.Header = "Quit Service";
            _quitLaunchItem.IsEnabled = true;
        }

        if (_startStopItem != null)
        {
            if (snapshot.IsRunning)
            {
                _startStopItem.Header = "Stop Workers";
                _startStopItem.IsEnabled = true;
            }
            else
            {
                _startStopItem.Header = "Start Workers";
                _startStopItem.IsEnabled = true;
            }
        }
    }
}
