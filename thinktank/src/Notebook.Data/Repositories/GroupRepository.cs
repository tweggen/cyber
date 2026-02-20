using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class GroupRepository(NotebookDbContext db) : IGroupRepository
{
    public async Task<GroupEntity> CreateAsync(Guid orgId, string name, CancellationToken ct)
    {
        var group = new GroupEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = name,
            Created = DateTimeOffset.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return group;
    }

    public Task<GroupEntity?> GetAsync(Guid groupId, CancellationToken ct)
        => db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<List<GroupEntity>> ListByOrgAsync(Guid orgId, CancellationToken ct)
        => db.Groups
            .Where(g => g.OrganizationId == orgId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return false;

        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GroupMemberEntity> AddMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct)
    {
        // Ensure author exists
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [authorId, authorId], ct);

        var existing = await db.GroupMembers.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.AuthorId == authorId, ct);

        if (existing is not null)
            return existing;

        var member = new GroupMemberEntity
        {
            GroupId = groupId,
            AuthorId = authorId,
            Joined = DateTimeOffset.UtcNow,
        };
        db.GroupMembers.Add(member);
        await db.SaveChangesAsync(ct);
        return member;
    }

    public async Task<bool> RemoveMemberAsync(Guid groupId, byte[] authorId, CancellationToken ct)
    {
        var member = await db.GroupMembers.FirstOrDefaultAsync(
            m => m.GroupId == groupId && m.AuthorId == authorId, ct);
        if (member is null) return false;

        db.GroupMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<GroupMemberEntity>> ListMembersAsync(Guid groupId, CancellationToken ct)
        => db.GroupMembers
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.Joined)
            .ToListAsync(ct);

    public async Task<GroupEdgeEntity> AddEdgeAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var edge = new GroupEdgeEntity
        {
            ParentGroupId = parentId,
            ChildGroupId = childId,
            Created = DateTimeOffset.UtcNow,
        };
        db.GroupEdges.Add(edge);
        await db.SaveChangesAsync(ct);
        return edge;
    }

    public async Task<bool> RemoveEdgeAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var edge = await db.GroupEdges.FirstOrDefaultAsync(
            e => e.ParentGroupId == parentId && e.ChildGroupId == childId, ct);
        if (edge is null) return false;

        db.GroupEdges.Remove(edge);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<GroupEdgeEntity>> ListEdgesAsync(Guid orgId, CancellationToken ct)
        => db.GroupEdges
            .Where(e => db.Groups.Any(g => g.Id == e.ParentGroupId && g.OrganizationId == orgId))
            .OrderBy(e => e.Created)
            .ToListAsync(ct);

    /// <summary>
    /// BFS from childId following child→parent edges. If parentId is reachable as an ancestor
    /// of childId, then adding parent→child would create a cycle.
    /// </summary>
    public async Task<bool> WouldCreateCycleAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        // If adding parent→child, we need to check: is parentId reachable from childId
        // by following existing parent→child edges upward (child→parent)?
        // Actually: we follow from parentId upward through existing edges where parentId is a child.
        // If we reach childId, then childId is an ancestor of parentId, so adding parent→child creates a cycle.
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == childId)
                return true;

            if (!visited.Add(current))
                continue;

            // Find all parents of 'current' (edges where current is the child)
            var parents = await db.GroupEdges
                .Where(e => e.ChildGroupId == current)
                .Select(e => e.ParentGroupId)
                .ToListAsync(ct);

            foreach (var p in parents)
                queue.Enqueue(p);
        }

        return false;
    }
}
