using System.Runtime.InteropServices;
using System.Text.Json;
using YourCyber.Models;

namespace YourCyber.Services;

public static class ProfileService
{
    private const string AppName = "YourCyber";
    private const string FileName = "profiles.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ProfileStore Load()
    {
        var path = GetProfilePath();
        if (!File.Exists(path))
            return new ProfileStore();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProfileStore>(json, ReadOptions) ?? new ProfileStore();
        }
        catch
        {
            return new ProfileStore();
        }
    }

    public static void Save(ProfileStore store)
    {
        var path = GetProfilePath();
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(store, WriteOptions);
        var tmpPath = path + ".tmp";

        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, path, overwrite: true);
    }

    private static string GetProfilePath()
    {
        return Path.Combine(GetConfigDir(), FileName);
    }

    private static string GetConfigDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName, "Config");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", AppName, "Config");

        // Linux
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(xdgConfig, AppName);
    }
}
