using Microsoft.AspNetCore.SignalR;
using Serilog;
using ThinkerAgent;
using ThinkerAgent.Auth;
using ThinkerAgent.Configuration;
using ThinkerAgent.Hubs;
using ThinkerAgent.Services;
using ThinkerAgent.Tools;

// Bootstrap: detect environment early so log dir resolves correctly.
// IsDevelopment defaults to false here; updated below once the host builder is available.
var logDir = EnvironmentDetector.GetLogDir("ThinkerAgent");

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

    // Propagate environment flag so EnvironmentDetector picks the right directories
    EnvironmentDetector.IsDevelopment = builder.Environment.IsDevelopment();

    builder.Host.UseSerilog();

    if (OperatingSystem.IsWindows())
        builder.Host.UseWindowsService();
    else if (OperatingSystem.IsLinux())
        builder.Host.UseSystemd();

    builder.WebHost.UseUrls("http://localhost:5948");

    // Layered configuration: appsettings → config.json → env → CLI
    var configHelper = new ConfigHelper<ThinkerOptions>(
        "ThinkerAgent", args, builder.Environment.EnvironmentName);
    builder.Configuration.AddConfiguration(configHelper.Configuration);
    builder.Services.AddSingleton(configHelper);

    Log.Information("Using configDirectory {ConfigDir}", Path.GetDirectoryName(configHelper.ConfigPath));
    Log.Information("envName = {Env}", builder.Environment.EnvironmentName);

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
        Microsoft.Extensions.Options.IOptionsSnapshot<ThinkerOptions> opts) =>
        Results.Ok(opts.Value));

    app.MapPut("/config", async (ThinkerOptions newConfig,
        ConfigHelper<ThinkerOptions> helper) =>
    {
        await helper.Save(newConfig);
        return Results.Ok(new { status = "saved", restart_required = false });
    }).AddEndpointFilter<AgentSecretFilter>();

    app.MapPost("/start", (WorkerState ws) =>
    {
        // Workers are started via BackgroundService on host start.
        // This endpoint is a placeholder for Phase 2 dynamic start/stop.
        return Results.Ok(new { status = "running" });
    }).AddEndpointFilter<AgentSecretFilter>();

    app.MapPost("/stop", (WorkerState ws) =>
    {
        // Placeholder for Phase 2 dynamic start/stop.
        return Results.Ok(new { status = "stop requested" });
    }).AddEndpointFilter<AgentSecretFilter>();

    app.MapPost("/quit", (IHostApplicationLifetime lifetime) =>
    {
        lifetime.StopApplication();
        return Results.Ok(new { status = "shutting down" });
    }).AddEndpointFilter<AgentSecretFilter>();

    app.MapGet("/models", async (ILlmClient llmClient, CancellationToken ct) =>
    {
        try
        {
            var models = await llmClient.ListModelsAsync(ct);
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
