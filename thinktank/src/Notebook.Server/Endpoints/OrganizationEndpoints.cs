using Microsoft.AspNetCore.Mvc;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Models;
using Notebook.Server.Services;

namespace Notebook.Server.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder routes)
    {
        // Organizations
        routes.MapPost("/organizations", CreateOrganization)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/organizations", ListOrganizations)
            .RequireAuthorization("CanRead");

        // Groups
        routes.MapPost("/organizations/{orgId}/groups", CreateGroup)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/organizations/{orgId}/groups", ListGroups)
            .RequireAuthorization("CanRead");
        routes.MapDelete("/groups/{groupId}", DeleteGroup)
            .RequireAuthorization("CanAdmin");

        // Edges
        routes.MapPost("/organizations/{orgId}/edges", AddEdge)
            .RequireAuthorization("CanAdmin");
        routes.MapDelete("/groups/{parentId}/edges/{childId}", RemoveEdge)
            .RequireAuthorization("CanAdmin");

        // Memberships
        routes.MapPost("/groups/{groupId}/members", AddMember)
            .RequireAuthorization("CanAdmin");
        routes.MapDelete("/groups/{groupId}/members/{authorIdHex}", RemoveMember)
            .RequireAuthorization("CanAdmin");
        routes.MapGet("/groups/{groupId}/members", ListMembers)
            .RequireAuthorization("CanRead");

        // Notebook assignment
        routes.MapPut("/notebooks/{notebookId}/group", AssignNotebookToGroup)
            .RequireAuthorization("CanAdmin");
    }

    // ── Organizations ──

    private static async Task<IResult> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var org = await orgRepo.CreateOrganizationAsync(request.Name.Trim(), ct);

        AuditHelper.LogAction(audit, httpContext, "organization.create",
            targetType: "organization", targetId: org.Id.ToString(),
            detail: new { name = org.Name });

        return Results.Created($"/organizations/{org.Id}", new OrganizationResponse
        {
            Id = org.Id,
            Name = org.Name,
            Created = org.Created,
        });
    }

    private static async Task<IResult> ListOrganizations(
        IOrganizationRepository orgRepo,
        CancellationToken ct)
    {
        var orgs = await orgRepo.ListOrganizationsAsync(ct);

        return Results.Ok(new ListOrganizationsResponse
        {
            Organizations = orgs.Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name,
                Created = o.Created,
            }).ToList(),
        });
    }

    // ── Groups ──

    private static async Task<IResult> CreateGroup(
        Guid orgId,
        [FromBody] CreateGroupRequest request,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "name is required" });

        var org = await orgRepo.GetOrganizationAsync(orgId, ct);
        if (org is null)
            return Results.NotFound(new { error = $"Organization {orgId} not found" });

        var group = await orgRepo.CreateGroupAsync(orgId, request.Name.Trim(), ct);

        // If a parent was specified, add the edge
        if (request.ParentId.HasValue)
        {
            var edgeAdded = await orgRepo.AddEdgeAsync(request.ParentId.Value, group.Id, ct);
            if (!edgeAdded)
                return Results.BadRequest(new { error = "Could not add parent edge (invalid parent or cycle)" });
        }

        AuditHelper.LogAction(audit, httpContext, "group.create",
            targetType: "group", targetId: group.Id.ToString(),
            detail: new { name = group.Name, organization_id = orgId });

        return Results.Created($"/groups/{group.Id}", new GroupResponse
        {
            Id = group.Id,
            OrganizationId = group.OrganizationId,
            Name = group.Name,
            Created = group.Created,
        });
    }

    private static async Task<IResult> ListGroups(
        Guid orgId,
        IOrganizationRepository orgRepo,
        CancellationToken ct)
    {
        var org = await orgRepo.GetOrganizationAsync(orgId, ct);
        if (org is null)
            return Results.NotFound(new { error = $"Organization {orgId} not found" });

        var groups = await orgRepo.ListGroupsAsync(orgId, ct);
        var edges = await orgRepo.ListEdgesAsync(orgId, ct);

        return Results.Ok(new ListGroupsResponse
        {
            Groups = groups.Select(g => new GroupResponse
            {
                Id = g.Id,
                OrganizationId = g.OrganizationId,
                Name = g.Name,
                Created = g.Created,
            }).ToList(),
            Edges = edges.Select(e => new EdgeResponse
            {
                ParentId = e.ParentId,
                ChildId = e.ChildId,
            }).ToList(),
        });
    }

    private static async Task<IResult> DeleteGroup(
        Guid groupId,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var deleted = await orgRepo.DeleteGroupAsync(groupId, ct);
        if (!deleted)
            return Results.NotFound(new { error = $"Group {groupId} not found" });

        AuditHelper.LogAction(audit, httpContext, "group.delete",
            targetType: "group", targetId: groupId.ToString());

        return Results.Ok(new { id = groupId, message = "Group deleted" });
    }

    // ── Edges ──

    private static async Task<IResult> AddEdge(
        Guid orgId,
        [FromBody] AddEdgeRequest request,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var added = await orgRepo.AddEdgeAsync(request.ParentId, request.ChildId, ct);
        if (!added)
            return Results.BadRequest(new { error = "Cannot add edge: invalid groups, cross-org, or would create cycle" });

        AuditHelper.LogAction(audit, httpContext, "group.edge.add",
            targetType: "group_edge",
            detail: new { parent_id = request.ParentId, child_id = request.ChildId });

        return Results.Ok(new EdgeResponse { ParentId = request.ParentId, ChildId = request.ChildId });
    }

    private static async Task<IResult> RemoveEdge(
        Guid parentId,
        Guid childId,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var removed = await orgRepo.RemoveEdgeAsync(parentId, childId, ct);
        if (!removed)
            return Results.NotFound(new { error = "Edge not found" });

        AuditHelper.LogAction(audit, httpContext, "group.edge.remove",
            targetType: "group_edge",
            detail: new { parent_id = parentId, child_id = childId });

        return Results.Ok(new { message = "Edge removed" });
    }

    // ── Memberships ──

    private static async Task<IResult> AddMember(
        Guid groupId,
        [FromBody] AddMemberRequest request,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AuthorId))
            return Results.BadRequest(new { error = "author_id is required" });

        if (request.Role is not ("member" or "admin"))
            return Results.BadRequest(new { error = "role must be 'member' or 'admin'" });

        var group = await orgRepo.GetGroupAsync(groupId, ct);
        if (group is null)
            return Results.NotFound(new { error = $"Group {groupId} not found" });

        var callerHex = httpContext.User.FindFirst("sub")?.Value;
        byte[]? grantedBy = null;
        if (!string.IsNullOrEmpty(callerHex))
            grantedBy = Convert.FromHexString(callerHex);

        var authorId = Convert.FromHexString(request.AuthorId);
        var membership = await orgRepo.AddMemberAsync(groupId, authorId, request.Role, grantedBy, ct);

        AuditHelper.LogAction(audit, httpContext, "group.member.add",
            targetType: "group_membership",
            detail: new { group_id = groupId, author_id = request.AuthorId, role = request.Role });

        return Results.Ok(new MemberResponse
        {
            AuthorId = Convert.ToHexString(membership.AuthorId).ToLowerInvariant(),
            GroupId = membership.GroupId,
            Role = membership.Role,
            Granted = membership.Granted,
            GrantedBy = membership.GrantedBy is not null
                ? Convert.ToHexString(membership.GrantedBy).ToLowerInvariant()
                : null,
        });
    }

    private static async Task<IResult> RemoveMember(
        Guid groupId,
        string authorIdHex,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorId = Convert.FromHexString(authorIdHex);
        var removed = await orgRepo.RemoveMemberAsync(groupId, authorId, ct);
        if (!removed)
            return Results.NotFound(new { error = "Membership not found" });

        AuditHelper.LogAction(audit, httpContext, "group.member.remove",
            targetType: "group_membership",
            detail: new { group_id = groupId, author_id = authorIdHex });

        return Results.Ok(new { message = "Member removed" });
    }

    private static async Task<IResult> ListMembers(
        Guid groupId,
        IOrganizationRepository orgRepo,
        CancellationToken ct)
    {
        var group = await orgRepo.GetGroupAsync(groupId, ct);
        if (group is null)
            return Results.NotFound(new { error = $"Group {groupId} not found" });

        var members = await orgRepo.ListMembersAsync(groupId, ct);

        return Results.Ok(new ListMembersResponse
        {
            Members = members.Select(m => new MemberResponse
            {
                AuthorId = Convert.ToHexString(m.AuthorId).ToLowerInvariant(),
                GroupId = m.GroupId,
                Role = m.Role,
                Granted = m.Granted,
                GrantedBy = m.GrantedBy is not null
                    ? Convert.ToHexString(m.GrantedBy).ToLowerInvariant()
                    : null,
            }).ToList(),
        });
    }

    // ── Notebook Assignment ──

    private static async Task<IResult> AssignNotebookToGroup(
        Guid notebookId,
        [FromBody] AssignGroupRequest request,
        IOrganizationRepository orgRepo,
        IAuditService audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();
        var authorId = Convert.FromHexString(authorHex);

        var assigned = await orgRepo.AssignNotebookToGroupAsync(notebookId, request.GroupId, authorId, ct);
        if (!assigned)
            return Results.NotFound(new { error = "Notebook not found, not owned by you, or group not found" });

        AuditHelper.LogAction(audit, httpContext, "notebook.assign_group", notebookId,
            targetType: "notebook", targetId: notebookId.ToString(),
            detail: new { group_id = request.GroupId });

        return Results.Ok(new { notebook_id = notebookId, group_id = request.GroupId });
    }
}
