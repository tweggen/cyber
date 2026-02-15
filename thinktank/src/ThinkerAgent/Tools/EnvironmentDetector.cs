using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThinkerAgent.Tools;

/// <summary>
/// Detects runtime environment (interactive dev vs installed service) and resolves
/// platform-aware directories for configuration and logs.
/// </summary>
public static class EnvironmentDetector
{
    /// <summary>
    /// Set from <c>builder.Environment.IsDevelopment()</c> during startup.
    /// </summary>
    public static bool IsDevelopment { get; set; }

    public static bool IsInteractiveDev()
        => Debugger.IsAttached || Environment.UserInteractive || IsDevelopment;

    // ── Config directories ──────────────────────────────────────────

    public static string GetConfigDir(string appName)
        => IsInteractiveDev() ? GetUserConfigDir(appName) : GetServiceConfigDir(appName);

    private static string GetUserConfigDir(string appName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName, "Config");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", appName, "Config");

        // Linux / others
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(xdgConfig, appName);
    }

    private static string GetServiceConfigDir(string appName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                appName, "Config");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine("/Library", "Application Support", appName, "Config");

        // Linux
        return Path.Combine("/etc", appName);
    }

    // ── Log directories ─────────────────────────────────────────────

    public static string GetLogDir(string appName)
        => IsInteractiveDev() ? GetUserLogDir(appName) : GetServiceLogDir(appName);

    private static string GetUserLogDir(string appName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName, "Logs");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", appName);

        // Linux
        var xdgState = Environment.GetEnvironmentVariable("XDG_STATE_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        return Path.Combine(xdgState, appName, "Logs");
    }

    private static string GetServiceLogDir(string appName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                appName, "Logs");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine("/Library", "Application Support", appName, "Logs");

        // Linux
        return Path.Combine("/var", "log", appName);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool DirectoryExistsOrCreatable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
