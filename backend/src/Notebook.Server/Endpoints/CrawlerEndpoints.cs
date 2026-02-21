using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class CrawlerEndpoints
{
    public static void MapCrawlerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/crawlers")
            .RequireAuthorization("CanAdmin");

        // Configuration endpoints
        group.MapPost("/{notebookId}/confluence/config", ConfigureConfluenceCrawler)
            .WithName("ConfigureConfluenceCrawler");

        group.MapPost("/confluence/test", TestConfluenceCrawler)
            .WithName("TestConfluenceCrawler");

        group.MapPost("/{notebookId}/confluence/run", RunConfluenceCrawler)
            .WithName("RunConfluenceCrawler");

        group.MapGet("/{notebookId}/runs", GetCrawlerRuns)
            .WithName("GetCrawlerRuns");

        group.MapGet("/{notebookId}/confluence/config", GetCrawlerConfig)
            .WithName("GetCrawlerConfig");
    }

    private static async Task<IResult> ConfigureConfluenceCrawler(
        Guid notebookId,
        [FromBody] ConfluenceCrawlerConfigRequest request,
        [FromQuery] string? orgId,
        CrawlerService crawlerService,
        HttpContext httpContext,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ConfigJson))
            return Results.BadRequest(new { message = "Configuration JSON is required" });

        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var organizationId = httpContext.User.FindFirst("organization_id")?.Value;

        // In dev/test mode without auth, use query parameter
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(organizationId))
        {
            if (!config.GetValue<bool>("AllowDevIdentity"))
                return Results.Unauthorized();

            // Use placeholder GUIDs for testing unless orgId is provided via query param
            userId = Guid.Empty.ToString();
            organizationId = orgId ?? Guid.Empty.ToString();
        }

        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(organizationId, out var orgGuid))
            return Results.Unauthorized();

        var result = await crawlerService.ConfigureConfluenceCrawlerAsync(
            notebookId, request.ConfigJson, userGuid, orgGuid);

        if (!result.Success)
            return Results.BadRequest(result);

        return Results.Ok(result);
    }

    private static async Task<IResult> TestConfluenceCrawler(
        [FromBody] ConfluenceCrawlerConfigRequest request,
        CrawlerService crawlerService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ConfigJson))
            return Results.BadRequest(new { message = "Configuration JSON is required" });

        var result = await crawlerService.TestConfluenceCrawlerAsync(request.ConfigJson);

        if (!result.Success)
            return Results.BadRequest(result);

        return Results.Ok(result);
    }

    private static async Task<IResult> RunConfluenceCrawler(
        Guid notebookId,
        CrawlerService crawlerService,
        CancellationToken ct)
    {
        var result = await crawlerService.RunConfluenceCrawlerAsync(notebookId);

        if (!result.Success)
            return Results.BadRequest(result);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetCrawlerRuns(
        Guid notebookId,
        CrawlerService crawlerService,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > 500)
            return Results.BadRequest(new { message = "Limit must be between 1 and 500" });

        var runs = await crawlerService.GetCrawlerRunsAsync(notebookId, limit);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetCrawlerConfig(
        Guid notebookId,
        CrawlerService crawlerService,
        CancellationToken ct)
    {
        var result = await crawlerService.GetCrawlerConfigAsync(notebookId);

        if (!result.Success)
            return Results.NotFound(result);

        return Results.Ok(result);
    }
}

public class ConfluenceCrawlerConfigRequest
{
    [JsonPropertyName("config_json")]
    public required string ConfigJson { get; set; }
}
