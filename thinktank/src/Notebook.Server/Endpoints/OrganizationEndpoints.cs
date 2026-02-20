using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/organizations")
            .RequireAuthorization("CanAdmin");

        group.MapGet("/", ListOrganizations);
        group.MapPost("/", CreateOrganization);
        group.MapGet("/{orgId}", GetOrganization);
        group.MapDelete("/{orgId}", DeleteOrganization);
        group.MapPatch("/{orgId}", RenameOrganization);
        group.MapGet("/{orgId}/members", ListMembers);
        group.MapPost("/{orgId}/members", AddMember);
        group.MapDelete("/{orgId}/members/{authorHex}", RemoveMember);
    }

    private static (byte[]? id, string? hex) GetCaller(HttpContext httpContext)
    {
        var hex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(hex)) return (null, null);
        return (Convert.FromHexString(hex), hex);
    }

    private static async Task<IResult> ListOrganizations(
        IOrganizationRepository orgRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var orgs = await orgRepo.ListByAuthorAsync(callerId, ct);
        return Results.Ok(new ListOrganizationsResponse
        {
            Organizations = orgs.Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name,
                Owner = Convert.ToHexString(o.OwnerId).ToLowerInvariant(),
                Created = o.Created,
            }).ToList(),
        });
    }

    private static async Task<IResult> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        IOrganizationRepository orgRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var org = await orgRepo.CreateAsync(request.Name.Trim(), callerId, ct);

        auditService.Log(callerId, "org.create", $"org:{org.Id}",
            new { name = org.Name },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Created($"/organizations/{org.Id}", new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            Owner = Convert.ToHexString(org.OwnerId).ToLowerInvariant(),
            Created = org.Created,
        });
    }

    private static async Task<IResult> GetOrganization(
        Guid orgId,
        IOrganizationRepository orgRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        // Must be org member
        var membership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (membership is null) return Results.NotFound();

        var org = await orgRepo.GetAsync(orgId, ct);
        if (org is null) return Results.NotFound();

        return Results.Ok(new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            Owner = Convert.ToHexString(org.OwnerId).ToLowerInvariant(),
            Created = org.Created,
        });
    }

    private static async Task<IResult> DeleteOrganization(
        Guid orgId,
        IOrganizationRepository orgRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var deleted = await orgRepo.DeleteAsync(orgId, callerId, ct);
        if (!deleted) return Results.NotFound();

        auditService.Log(callerId, "org.delete", $"org:{orgId}",
            detail: null,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new { id = orgId, message = "Organization deleted" });
    }

    private static async Task<IResult> RenameOrganization(
        Guid orgId,
        [FromBody] RenameOrganizationRequest request,
        IOrganizationRepository orgRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var org = await orgRepo.RenameAsync(orgId, request.Name.Trim(), callerId, ct);
        if (org is null) return Results.NotFound();

        auditService.Log(callerId, "org.rename", $"org:{orgId}",
            new { name = org.Name },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            Owner = Convert.ToHexString(org.OwnerId).ToLowerInvariant(),
            Created = org.Created,
        });
    }

    private static async Task<IResult> ListMembers(
        Guid orgId,
        IOrganizationRepository orgRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        // Must be org member
        var membership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (membership is null) return Results.NotFound();

        var members = await orgRepo.ListMembersAsync(orgId, ct);
        return Results.Ok(new ListOrgMembersResponse
        {
            Members = members.Select(m => new OrgMemberResponse
            {
                OrganizationId = m.OrganizationId,
                AuthorId = Convert.ToHexString(m.AuthorId).ToLowerInvariant(),
                Role = m.Role,
                Joined = m.Joined,
            }).ToList(),
        });
    }

    private static async Task<IResult> AddMember(
        Guid orgId,
        [FromBody] AddOrgMemberRequest request,
        IOrganizationRepository orgRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        // Must be owner or admin
        var callerMembership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.AuthorId) || request.AuthorId.Length != 64)
            return Results.BadRequest(new { error = "author_id must be a 64-character hex string" });

        var validRoles = new[] { "owner", "admin", "member" };
        if (!validRoles.Contains(request.Role))
            return Results.BadRequest(new { error = "role must be owner, admin, or member" });

        byte[] targetId;
        try { targetId = Convert.FromHexString(request.AuthorId); }
        catch (FormatException) { return Results.BadRequest(new { error = "author_id is not valid hex" }); }

        var member = await orgRepo.AddMemberAsync(orgId, targetId, request.Role, ct);

        auditService.Log(callerId, "org.member.add", $"org:{orgId}",
            new { target = request.AuthorId.ToLowerInvariant(), role = request.Role },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new OrgMemberResponse
        {
            OrganizationId = member.OrganizationId,
            AuthorId = Convert.ToHexString(member.AuthorId).ToLowerInvariant(),
            Role = member.Role,
            Joined = member.Joined,
        });
    }

    private static async Task<IResult> RemoveMember(
        Guid orgId,
        string authorHex,
        IOrganizationRepository orgRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        // Must be owner or admin
        var callerMembership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        byte[] targetId;
        try { targetId = Convert.FromHexString(authorHex); }
        catch (FormatException) { return Results.BadRequest(new { error = "authorHex is not valid hex" }); }

        var removed = await orgRepo.RemoveMemberAsync(orgId, targetId, ct);
        if (!removed) return Results.NotFound();

        auditService.Log(callerId, "org.member.remove", $"org:{orgId}",
            new { target = authorHex.ToLowerInvariant() },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new { organization_id = orgId, author_id = authorHex.ToLowerInvariant(), message = "Member removed" });
    }
}
