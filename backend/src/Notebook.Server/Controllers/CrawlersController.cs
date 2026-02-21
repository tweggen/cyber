using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notebook.Server.Services;
using Notebook.Server.Services.Crawlers;
using System.Security.Claims;

namespace Notebook.Server.Controllers;

/// <summary>
/// API endpoints for crawler management and execution.
/// Supports configuration, testing, running, and monitoring crawlers.
/// </summary>
[ApiController]
[Route("api/crawlers")]
[Authorize]
public sealed class CrawlersController : ControllerBase
{
    private readonly CrawlerService _crawlerService;
    private readonly ILogger<CrawlersController> _logger;

    public CrawlersController(
        CrawlerService crawlerService,
        ILogger<CrawlersController> logger)
    {
        _crawlerService = crawlerService;
        _logger = logger;
    }

    /// <summary>
    /// Configure or update a Confluence crawler for a notebook.
    /// </summary>
    /// <param name="notebookId">Notebook ID</param>
    /// <param name="configJson">Confluence crawler configuration as JSON</param>
    /// <returns>Configuration response</returns>
    [HttpPost("{notebookId}/confluence/config")]
    public async Task<ActionResult<CrawlerConfigResponse>> ConfigureConfluenceCrawler(
        Guid notebookId,
        [FromBody] ConfluenceCrawlerConfigRequest request)
    {
        var userId = GetUserId();
        var organizationId = GetOrganizationId();

        if (string.IsNullOrWhiteSpace(request.ConfigJson))
            return BadRequest(new { message = "Configuration JSON is required" });

        var result = await _crawlerService.ConfigureConfluenceCrawlerAsync(
            notebookId, request.ConfigJson, userId, organizationId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Test Confluence crawler connection and configuration.
    /// </summary>
    /// <param name="request">Configuration to test</param>
    /// <returns>Test result with space information</returns>
    [HttpPost("confluence/test")]
    public async Task<ActionResult<CrawlerTestResponse>> TestConfluenceCrawler(
        [FromBody] ConfluenceCrawlerConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfigJson))
            return BadRequest(new { message = "Configuration JSON is required" });

        var result = await _crawlerService.TestConfluenceCrawlerAsync(request.ConfigJson);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Run a Confluence crawler to fetch and ingest pages.
    /// </summary>
    /// <param name="notebookId">Notebook ID</param>
    /// <returns>Run result with statistics</returns>
    [HttpPost("{notebookId}/confluence/run")]
    public async Task<ActionResult<CrawlerRunResponse>> RunConfluenceCrawler(Guid notebookId)
    {
        var result = await _crawlerService.RunConfluenceCrawlerAsync(notebookId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get crawler run history for a notebook.
    /// </summary>
    /// <param name="notebookId">Notebook ID</param>
    /// <param name="limit">Maximum number of runs to return (default: 50)</param>
    /// <returns>List of run history entries</returns>
    [HttpGet("{notebookId}/runs")]
    public async Task<ActionResult<List<CrawlerRunHistory>>> GetCrawlerRuns(
        Guid notebookId,
        [FromQuery] int limit = 50)
    {
        if (limit < 1 || limit > 500)
            return BadRequest(new { message = "Limit must be between 1 and 500" });

        var runs = await _crawlerService.GetCrawlerRunsAsync(notebookId, limit);
        return Ok(runs);
    }

    /// <summary>
    /// Get the current crawler configuration for a notebook.
    /// </summary>
    /// <param name="notebookId">Notebook ID</param>
    /// <returns>Crawler configuration</returns>
    [HttpGet("{notebookId}/confluence/config")]
    public async Task<ActionResult<CrawlerConfigResponse>> GetCrawlerConfig(Guid notebookId)
    {
        var result = await _crawlerService.GetCrawlerConfigAsync(notebookId);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    // ============= Helper Methods =============

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    private Guid GetOrganizationId()
    {
        // Try to get organization ID from claims
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgIdClaim, out var orgId) ? orgId : Guid.Empty;
    }
}

// ============= Request DTOs =============

public class ConfluenceCrawlerConfigRequest
{
    public required string ConfigJson { get; set; }
}
