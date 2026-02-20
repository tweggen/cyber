using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class OrganizationRepository(NotebookDbContext db) : IOrganizationRepository
{
    // ── Organizations ──

    public async Task<OrganizationEntity> CreateOrganizationAsync(string name, CancellationToken ct)
    {
        var org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Created = DateTimeOffset.UtcNow,
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);
        return org;
    }

    public Task<List<OrganizationEntity>> ListOrganizationsAsync(CancellationToken ct)
        => db.Organizations.OrderBy(o => o.Name).ToListAsync(ct);

    public Task<OrganizationEntity?> GetOrganizationAsync(Guid orgId, CancellationToken ct)
        => db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);

    // ── Groups ──

    public async Task<GroupEntity> CreateGroupAsync(Guid organizationId, string name, CancellationToken ct)
    {
        var group = new GroupEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Created = DateTimeOffset.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return group;
    }

    public Task<List<GroupEntity>> ListGroupsAsync(Guid organizationId, CancellationToken ct)
        => db.Groups.Where(g => g.OrganizationId == organizationId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    public Task<GroupEntity?> GetGroupAsync(Guid groupId, CancellationToken ct)
        => db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return false;

        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── DAG Edges ──

    /// <summary>
    /// Add a parent→child edge. Returns false if adding the edge would create a cycle.
    /// Uses a recursive CTE to walk ancestors of the proposed parent and check if the
    /// proposed child is reachable (which would form a cycle).
    /// </summary>
    public async Task<bool> AddEdgeAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        if (parentId == childId) return false;

        // Verify both groups exist and belong to the same organization
        var parent = await db.Groups.FirstOrDefaultAsync(g => g.Id == parentId, ct);
        var child = await db.Groups.FirstOrDefaultAsync(g => g.Id == childId, ct);
        if (parent is null || child is null) return false;
        if (parent.OrganizationId != child.OrganizationId) return false;

        // Check if edge already exists
        var exists = await db.GroupEdges.AnyAsync(
            e => e.ParentId == parentId && e.ChildId == childId, ct);
        if (exists) return true; // idempotent

        // Cycle detection: walk ancestors of the proposed parent.
        // If the proposed child is an ancestor of the parent, adding parent→child creates a cycle.
        var wouldCycle = await db.Database
            .SqlQueryRaw<bool>(
                """
                WITH RECURSIVE ancestors AS (
                    SELECT parent_id FROM group_edges WHERE child_id = {0}
                    UNION
                    SELECT ge.parent_id FROM group_edges ge
                    JOIN ancestors a ON ge.child_id = a.parent_id
                )
                SELECT EXISTS (SELECT 1 FROM ancestors WHERE parent_id = {1}) AS "Value"
                """,
                parentId, childId)
            .FirstOrDefaultAsync(ct);

        if (wouldCycle) return false;

        db.GroupEdges.Add(new GroupEdgeEntity { ParentId = parentId, ChildId = childId });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveEdgeAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var edge = await db.GroupEdges.FirstOrDefaultAsync(
            e => e.ParentId == parentId && e.ChildId == childId, ct);
        if (edge is null) return false;

        db.GroupEdges.Remove(edge);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<GroupEdgeEntity>> ListEdgesAsync(Guid organizationId, CancellationToken ct)
        => db.GroupEdges
            .Where(e => db.Groups.Any(g => g.Id == e.ParentId && g.OrganizationId == organizationId))
            .ToListAsync(ct);

    // ── Memberships ──

    public async Task<GroupMembershipEntity> AddMemberAsync(
        Guid groupId, byte[] authorId, string role, byte[]? grantedBy, CancellationToken ct)
    {
        // Ensure the author exists (auto-create synthetic author row)
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [authorId, authorId], ct);

        var existing = await db.GroupMemberships.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.AuthorId == authorId, ct);

        if (existing is not null)
        {
            existing.Role = role;
            existing.Granted = DateTimeOffset.UtcNow;
            existing.GrantedBy = grantedBy;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var membership = new GroupMembershipEntity
        {
            AuthorId = authorId,
            GroupId = groupId,
            Role = role,
            Granted = DateTimeOffset.UtcNow,
            GrantedBy = grantedBy,
        };
        db.GroupMemberships.Add(membership);
        await db.SaveChangesAsync(ct);
        return membership;
    }

    public async Task<bool> RemoveMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct)
    {
        var membership = await db.GroupMemberships.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.AuthorId == authorId, ct);
        if (membership is null) return false;

        db.GroupMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<GroupMembershipEntity>> ListMembersAsync(Guid groupId, CancellationToken ct)
        => db.GroupMemberships.Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Granted)
            .ToListAsync(ct);

    public Task<List<GroupEntity>> ListGroupsForAuthorAsync(byte[] authorId, CancellationToken ct)
        => db.Groups
            .Where(g => db.GroupMemberships.Any(m => m.GroupId == g.Id && m.AuthorId == authorId))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    // ── Group Membership Lookup (recursive) ──

    public async Task<string?> GetGroupMembershipRoleAsync(
        Guid owningGroupId, byte[] authorId, CancellationToken ct)
    {
        // Walk the owning group and all its descendants, find the highest-priority
        // membership role for the given author.
        var role = await db.Database
            .SqlQueryRaw<string>(
                """
                WITH RECURSIVE group_tree AS (
                    SELECT id FROM groups WHERE id = {0}
                    UNION
                    SELECT ge.child_id FROM group_edges ge
                    JOIN group_tree gt ON ge.parent_id = gt.id
                )
                SELECT role AS "Value" FROM group_memberships
                WHERE author_id = {1} AND group_id IN (SELECT id FROM group_tree)
                ORDER BY CASE role WHEN 'admin' THEN 0 ELSE 1 END
                LIMIT 1
                """,
                owningGroupId, authorId)
            .FirstOrDefaultAsync(ct);

        return role;
    }

    // ── Notebook Ownership ──

    public async Task<bool> AssignNotebookToGroupAsync(
        Guid notebookId, Guid groupId, byte[] requestingAuthorId, CancellationToken ct)
    {
        var notebook = await db.Notebooks.FirstOrDefaultAsync(
            n => n.Id == notebookId && n.OwnerId == requestingAuthorId, ct);
        if (notebook is null) return false;

        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return false;

        notebook.OwningGroupId = groupId;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
