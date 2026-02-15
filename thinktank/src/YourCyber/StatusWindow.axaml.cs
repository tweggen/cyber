using Avalonia.Controls;
using Avalonia.Threading;
using YourCyber.ViewModels;

namespace YourCyber;

public partial class StatusWindow : Window
{
    private readonly DispatcherTimer _uptimeTimer;

    public StatusWindow() : this(new AgentStatusViewModel()) { }

    public StatusWindow(AgentStatusViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) =>
        {
            if (viewModel.IsRunning)
            {
                viewModel.UptimeSeconds += 1;
            }
        };
        _uptimeTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _uptimeTimer.Stop();
        base.OnClosed(e);
    }
}
