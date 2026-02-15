using System.Diagnostics;
using System.Runtime.Versioning;

namespace YourCyber.Platform;

[SupportedOSPlatform("windows")]
public class WindowsServiceLauncher : IServiceLauncher
{
    private const string ServiceName = "ThinkerAgent";

    public bool IsSupported => true;

    public Task<bool> TryLaunchAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"start {ServiceName}",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return Task.FromResult(false);

            process.WaitForExit(TimeSpan.FromSeconds(15));
            return Task.FromResult(process.ExitCode == 0);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch service: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}
