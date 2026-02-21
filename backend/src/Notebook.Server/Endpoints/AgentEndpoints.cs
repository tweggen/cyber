using Microsoft.AspNetCore.Mvc;
using Notebook.Core.Security;
using Notebook.Data.Entities;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class AgentEndpoints
{
    private static readonly HashSet<string> ValidLevels =
        ["PUBLIC", "INTERNAL", "CONFIDENTIAL", "SECRET", "TOP_SECRET"];

    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/agents", RegisterAgent).RequireAuthorization("CanAdmin");
        routes.MapGet("/agents", ListAgents).RequireAuthorization("CanRead");
        routes.MapGet("/agents/{agentId}", GetAgent).RequireAuthorization("CanRead");
        routes.MapPut("/agents/{agentId}", UpdateAgent).RequireAuthorization("CanAdmin");
        routes.MapDelete("/agents/{agentId}", DeleteAgent).RequireAuthorization("CanAdmin");
    }

    private static async Task<IResult> RegisterAgent(
        [FromBody] RegisterAgentRequest request,
        IAgentRepository agentRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
            return Results.BadRequest(new { error = "id is required" });

        if (!ValidLevels.Contains(request.MaxLevel))
            return Results.BadRequest(new { error = $"Invalid max_level: {request.MaxLevel}" });

        var existing = await agentRepo.GetAsync(request.Id, ct);
        if (existing is not null)
            return Results.Conflict(new { error = $"Agent '{request.Id}' already exists" });

        var agent = new AgentEntity
        {
            Id = request.Id,
            OrganizationId = request.OrganizationId,
            MaxLevel = request.MaxLevel,
            Compartments = request.Compartments,
            Infrastructure = request.Infrastructure,
            Registered = DateTimeOffset.UtcNow,
        };

        await agentRepo.RegisterAsync(agent, ct);

        await AuditHelper.LogActionAsync(audit, httpContext, "agent.register", null,
            targetType: "agent", targetId: request.Id,
            detail: new { max_level = request.MaxLevel, compartments = request.Compartments });

        return Results.Created($"/agents/{agent.Id}", ToResponse(agent));
    }

    private static async Task<IResult> ListAgents(
        IAgentRepository agentRepo,
        CancellationToken ct)
    {
        var agents = await agentRepo.ListAsync(ct);
        return Results.Ok(new ListAgentsResponse
        {
            Agents = agents.Select(ToResponse).ToList(),
        });
    }

    private static async Task<IResult> GetAgent(
        string agentId,
        IAgentRepository agentRepo,
        CancellationToken ct)
    {
        var agent = await agentRepo.GetAsync(agentId, ct);
        if (agent is null)
            return Results.NotFound(new { error = $"Agent '{agentId}' not found" });

        return Results.Ok(ToResponse(agent));
    }

    private static async Task<IResult> UpdateAgent(
        string agentId,
        [FromBody] UpdateAgentRequest request,
        IAgentRepository agentRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!ValidLevels.Contains(request.MaxLevel))
            return Results.BadRequest(new { error = $"Invalid max_level: {request.MaxLevel}" });

        var agent = await agentRepo.GetAsync(agentId, ct);
        if (agent is null)
            return Results.NotFound(new { error = $"Agent '{agentId}' not found" });

        agent.MaxLevel = request.MaxLevel;
        agent.Compartments = request.Compartments;
        agent.Infrastructure = request.Infrastructure;

        await agentRepo.UpdateAsync(agent, ct);

        await AuditHelper.LogActionAsync(audit, httpContext, "agent.update", null,
            targetType: "agent", targetId: agentId,
            detail: new { max_level = request.MaxLevel, compartments = request.Compartments });

        return Results.Ok(ToResponse(agent));
    }

    private static async Task<IResult> DeleteAgent(
        string agentId,
        IAgentRepository agentRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var deleted = await agentRepo.DeleteAsync(agentId, ct);
        if (!deleted)
            return Results.NotFound(new { error = $"Agent '{agentId}' not found" });

        await AuditHelper.LogActionAsync(audit, httpContext, "agent.delete", null,
            targetType: "agent", targetId: agentId);

        return Results.Ok(new { message = $"Agent '{agentId}' deleted" });
    }

    private static AgentResponse ToResponse(AgentEntity entity) => new()
    {
        Id = entity.Id,
        OrganizationId = entity.OrganizationId,
        MaxLevel = entity.MaxLevel,
        Compartments = entity.Compartments,
        Infrastructure = entity.Infrastructure,
        Registered = entity.Registered,
        LastSeen = entity.LastSeen,
    };
}
