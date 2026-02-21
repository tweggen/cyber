using System.Diagnostics;
using System.Runtime.Versioning;

namespace YourCyber.Platform;

[SupportedOSPlatform("macos")]
public class MacServiceLauncher : IServiceLauncher
{
    private const string DaemonLabel = "com.cyber.thinkeragent";

    public bool IsSupported => true;

    public Task<bool> TryLaunchAsync()
    {
        try
        {
            var userPlist = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents", $"{DaemonLabel}.plist");

            string arguments;
            bool needsElevation;

            if (File.Exists(userPlist))
            {
                arguments = $"load -w {userPlist}";
                needsElevation = false;
            }
            else
            {
                var systemPlist = $"/Library/LaunchDaemons/{DaemonLabel}.plist";
                arguments = $"load -w {systemPlist}";
                needsElevation = true;
            }

            ProcessStartInfo psi;
            if (needsElevation)
            {
                var script = $"do shell script \"launchctl {arguments}\" with administrator privileges";
                psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "/bin/launchctl",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
                return Task.FromResult(false);

            process.WaitForExit(TimeSpan.FromSeconds(15));
            return Task.FromResult(process.ExitCode == 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"launchctl error: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}
