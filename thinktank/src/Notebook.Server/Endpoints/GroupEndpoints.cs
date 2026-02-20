using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder routes)
    {
        var orgGroup = routes.MapGroup("/organizations/{orgId}/groups")
            .RequireAuthorization("CanAdmin");

        orgGroup.MapGet("/", ListGroups);
        orgGroup.MapPost("/", CreateGroup);

        var groupGroup = routes.MapGroup("/groups/{groupId}")
            .RequireAuthorization("CanAdmin");

        groupGroup.MapGet("/", GetGroup);
        groupGroup.MapDelete("/", DeleteGroup);
        groupGroup.MapGet("/members", ListGroupMembers);
        groupGroup.MapPost("/members", AddGroupMember);
        groupGroup.MapDelete("/members/{authorHex}", RemoveGroupMember);
        groupGroup.MapGet("/edges", ListEdges);
        groupGroup.MapPost("/edges", AddEdge);
        groupGroup.MapDelete("/edges/{childGroupId}", RemoveEdge);
    }

    private static (byte[]? id, string? hex) GetCaller(HttpContext httpContext)
    {
        var hex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(hex)) return (null, null);
        return (Convert.FromHexString(hex), hex);
    }

    // --- Org-scoped routes ---

    private static async Task<IResult> ListGroups(
        Guid orgId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var membership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (membership is null) return Results.NotFound();

        var groups = await groupRepo.ListByOrgAsync(orgId, ct);
        return Results.Ok(new ListGroupsResponse
        {
            Groups = groups.Select(g => new GroupResponse
            {
                Id = g.Id,
                OrganizationId = g.OrganizationId,
                Name = g.Name,
                Created = g.Created,
            }).ToList(),
        });
    }

    private static async Task<IResult> CreateGroup(
        Guid orgId,
        [FromBody] CreateGroupRequest request,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        // Must be owner or admin
        var callerMembership = await orgRepo.GetMemberAsync(orgId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        // Verify org exists
        var org = await orgRepo.GetAsync(orgId, ct);
        if (org is null) return Results.NotFound();

        var group = await groupRepo.CreateAsync(orgId, request.Name.Trim(), ct);

        auditService.Log(callerId, "group.create", $"group:{group.Id}",
            new { organization_id = orgId, name = group.Name },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Created($"/groups/{group.Id}", new GroupResponse
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Created = group.Created,
        });
    }

    // --- Group-scoped routes ---

    private static async Task<IResult> GetGroup(
        Guid groupId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        // Must be org member
        var membership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (membership is null) return Results.NotFound();

        return Results.Ok(new GroupResponse
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Created = group.Created,
        });
    }

    private static async Task<IResult> DeleteGroup(
        Guid groupId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        // Must be org owner or admin
        var callerMembership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        var deleted = await groupRepo.DeleteAsync(groupId, ct);
        if (!deleted) return Results.NotFound();

        auditService.Log(callerId, "group.delete", $"group:{groupId}",
            new { organization_id = group.OrganizationId },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new { id = groupId, message = "Group deleted" });
    }

    private static async Task<IResult> ListGroupMembers(
        Guid groupId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        var membership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (membership is null) return Results.NotFound();

        var members = await groupRepo.ListMembersAsync(groupId, ct);
        return Results.Ok(new ListGroupMembersResponse
        {
            Members = members.Select(m => new GroupMemberResponse
            {
                GroupId = m.GroupId,
                AuthorId = Convert.ToHexString(m.AuthorId).ToLowerInvariant(),
                Joined = m.Joined,
            }).ToList(),
        });
    }

    private static async Task<IResult> AddGroupMember(
        Guid groupId,
        [FromBody] AddGroupMemberRequest request,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        var callerMembership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.AuthorId) || request.AuthorId.Length != 64)
            return Results.BadRequest(new { error = "author_id must be a 64-character hex string" });

        byte[] targetId;
        try { targetId = Convert.FromHexString(request.AuthorId); }
        catch (FormatException) { return Results.BadRequest(new { error = "author_id is not valid hex" }); }

        var member = await groupRepo.AddMemberAsync(groupId, targetId, ct);

        auditService.Log(callerId, "group.member.add", $"group:{groupId}",
            new { target = request.AuthorId.ToLowerInvariant() },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new GroupMemberResponse
        {
            GroupId = member.GroupId,
            AuthorId = Convert.ToHexString(member.AuthorId).ToLowerInvariant(),
            Joined = member.Joined,
        });
    }

    private static async Task<IResult> RemoveGroupMember(
        Guid groupId,
        string authorHex,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        var callerMembership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        byte[] targetId;
        try { targetId = Convert.FromHexString(authorHex); }
        catch (FormatException) { return Results.BadRequest(new { error = "authorHex is not valid hex" }); }

        var removed = await groupRepo.RemoveMemberAsync(groupId, targetId, ct);
        if (!removed) return Results.NotFound();

        auditService.Log(callerId, "group.member.remove", $"group:{groupId}",
            new { target = authorHex.ToLowerInvariant() },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new { group_id = groupId, author_id = authorHex.ToLowerInvariant(), message = "Member removed" });
    }

    private static async Task<IResult> ListEdges(
        Guid groupId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        var membership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (membership is null) return Results.NotFound();

        var edges = await groupRepo.ListEdgesAsync(group.OrganizationId, ct);
        return Results.Ok(new ListGroupEdgesResponse
        {
            Edges = edges.Select(e => new GroupEdgeResponse
            {
                ParentGroupId = e.ParentGroupId,
                ChildGroupId = e.ChildGroupId,
                Created = e.Created,
            }).ToList(),
        });
    }

    private static async Task<IResult> AddEdge(
        Guid groupId,
        [FromBody] AddEdgeRequest request,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var parentGroup = await groupRepo.GetAsync(groupId, ct);
        if (parentGroup is null) return Results.NotFound();

        var callerMembership = await orgRepo.GetMemberAsync(parentGroup.OrganizationId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        // Verify child group exists and is in the same org
        var childGroup = await groupRepo.GetAsync(request.ChildGroupId, ct);
        if (childGroup is null || childGroup.OrganizationId != parentGroup.OrganizationId)
            return Results.BadRequest(new { error = "child group not found or not in the same organization" });

        if (groupId == request.ChildGroupId)
            return Results.BadRequest(new { error = "a group cannot be its own parent" });

        // Cycle detection
        var wouldCycle = await groupRepo.WouldCreateCycleAsync(groupId, request.ChildGroupId, ct);
        if (wouldCycle)
            return Results.Conflict(new { error = "adding this edge would create a cycle in the group DAG" });

        var edge = await groupRepo.AddEdgeAsync(groupId, request.ChildGroupId, ct);

        auditService.Log(callerId, "group.edge.add", $"group:{groupId}",
            new { parent_group_id = groupId, child_group_id = request.ChildGroupId },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new GroupEdgeResponse
        {
            ParentGroupId = edge.ParentGroupId,
            ChildGroupId = edge.ChildGroupId,
            Created = edge.Created,
        });
    }

    private static async Task<IResult> RemoveEdge(
        Guid groupId,
        Guid childGroupId,
        IOrganizationRepository orgRepo,
        IGroupRepository groupRepo,
        IAuditService auditService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var (callerId, _) = GetCaller(httpContext);
        if (callerId is null) return Results.Unauthorized();

        var group = await groupRepo.GetAsync(groupId, ct);
        if (group is null) return Results.NotFound();

        var callerMembership = await orgRepo.GetMemberAsync(group.OrganizationId, callerId, ct);
        if (callerMembership is null || callerMembership.Role == "member")
            return Results.NotFound();

        var removed = await groupRepo.RemoveEdgeAsync(groupId, childGroupId, ct);
        if (!removed) return Results.NotFound();

        auditService.Log(callerId, "group.edge.remove", $"group:{groupId}",
            new { parent_group_id = groupId, child_group_id = childGroupId },
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString());

        return Results.Ok(new { parent_group_id = groupId, child_group_id = childGroupId, message = "Edge removed" });
    }
}
