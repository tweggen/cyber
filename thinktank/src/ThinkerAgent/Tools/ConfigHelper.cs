using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThinkerAgent.Tools;

/// <summary>
/// Layered configuration with atomic persistence to a dedicated <c>config.json</c> file.
/// The layer order (last wins):
///   appsettings.json → appsettings.{env}.json → appsettings.{env}.{machine}.json
///   → config.json → environment variables → command-line args.
/// </summary>
public sealed class ConfigHelper<TOptions> where TOptions : class, new()
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;
    private readonly string _sectionName;

    public IConfigurationRoot Configuration { get; }

    public ConfigHelper(string appName, string[] args, string environmentName)
    {
        // Derive section name: "ThinkerOptions" → "Thinker"
        var typeName = typeof(TOptions).Name;
        _sectionName = typeName.EndsWith("Options", StringComparison.Ordinal)
            ? typeName[..^"Options".Length]
            : typeName;

        var configDir = EnvironmentDetector.GetConfigDir(appName);
        EnvironmentDetector.EnsureDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");

        var machineName = Environment.MachineName;

        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.{machineName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile(_configPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
    }

    /// <summary>
    /// Atomically writes the options to <c>config.json</c> under the section name.
    /// Uses a temp file + <see cref="File.Move(string,string,bool)"/> for crash safety.
    /// </summary>
    public async Task Save(TOptions options)
    {
        var wrapper = new Dictionary<string, TOptions>
        {
            [_sectionName] = options
        };

        var json = JsonSerializer.Serialize(wrapper, WriteOptions);
        var tmpPath = _configPath + ".tmp";

        await File.WriteAllTextAsync(tmpPath, json);
        File.Move(tmpPath, _configPath, overwrite: true);
    }

    public string ConfigPath => _configPath;
}
