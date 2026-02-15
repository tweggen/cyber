using Microsoft.AspNetCore.SignalR;
using Serilog;
using ThinkerAgent;
using ThinkerAgent.Hubs;
using ThinkerAgent.Services;

// Cross-platform log directory
var logDir = OperatingSystem.IsWindows()
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ThinkerAgent", "logs")
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".thinkeragent", "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDir, "thinkeragent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    if (OperatingSystem.IsWindows())
        builder.Host.UseWindowsService();
    else if (OperatingSystem.IsLinux())
        builder.Host.UseSystemd();

    builder.WebHost.UseUrls("http://localhost:5948");

    builder.Services.AddSignalR();
    builder.Services.AddThinkerServices(builder.Configuration);

    var app = builder.Build();

    // Wire SignalR push on state changes
    var state = app.Services.GetRequiredService<WorkerState>();
    var hubContext = app.Services.GetRequiredService<IHubContext<ThinkerControlHub>>();
    state.OnStateChanged += () =>
    {
        _ = hubContext.Clients.All.SendAsync("WorkerStateChanged", state.GetSnapshot());
    };

    app.MapHub<ThinkerControlHub>("/thinkercontrolhub");

    // REST endpoints
    app.MapGet("/status", (WorkerState ws) => Results.Ok(ws.GetSnapshot()));

    app.MapGet("/config", (
        Microsoft.Extensions.Options.IOptionsSnapshot<ThinkerAgent.Configuration.ThinkerOptions> opts) =>
        Results.Ok(opts.Value));

    app.MapPut("/config", async (ThinkerAgent.Configuration.ThinkerOptions newConfig) =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = await File.ReadAllTextAsync(path);
        var doc = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        doc["Thinker"] = System.Text.Json.JsonSerializer.SerializeToNode(newConfig);
        await File.WriteAllTextAsync(path, doc.ToJsonString(
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return Results.Ok(new { status = "saved", restart_required = true });
    });

    app.MapPost("/start", (WorkerState ws) =>
    {
        // Workers are started via BackgroundService on host start.
        // This endpoint is a placeholder for Phase 2 dynamic start/stop.
        return Results.Ok(new { status = "running" });
    });

    app.MapPost("/stop", (WorkerState ws) =>
    {
        // Placeholder for Phase 2 dynamic start/stop.
        return Results.Ok(new { status = "stop requested" });
    });

    app.MapPost("/quit", (IHostApplicationLifetime lifetime) =>
    {
        lifetime.StopApplication();
        return Results.Ok(new { status = "shutting down" });
    });

    app.MapGet("/models", async (IOllamaClient ollama, CancellationToken ct) =>
    {
        try
        {
            var models = await ollama.ListModelsAsync(ct);
            return Results.Ok(new { models });
        }
        catch
        {
            return Results.StatusCode(502);
        }
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ThinkerAgent terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
