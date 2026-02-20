using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class OrganizationRepository(NotebookDbContext db) : IOrganizationRepository
{
    public async Task<OrganizationEntity> CreateAsync(string name, byte[] ownerId, CancellationToken ct)
    {
        // Ensure author exists
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [ownerId, ownerId], ct);

        var org = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            Created = DateTimeOffset.UtcNow,
        };
        db.Organizations.Add(org);

        // Auto-create owner membership
        db.OrganizationMembers.Add(new OrganizationMemberEntity
        {
            OrganizationId = org.Id,
            AuthorId = ownerId,
            Role = "owner",
            Joined = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return org;
    }

    public Task<OrganizationEntity?> GetAsync(Guid orgId, CancellationToken ct)
        => db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);

    public Task<List<OrganizationEntity>> ListByAuthorAsync(byte[] authorId, CancellationToken ct)
        => db.Organizations
            .Where(o => db.OrganizationMembers.Any(m => m.OrganizationId == o.Id && m.AuthorId == authorId))
            .OrderBy(o => o.Created)
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(Guid orgId, byte[] requestingAuthorId, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(
            o => o.Id == orgId && o.OwnerId == requestingAuthorId, ct);
        if (org is null) return false;

        db.Organizations.Remove(org);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<OrganizationEntity?> RenameAsync(Guid orgId, string newName, byte[] requestingAuthorId, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(
            o => o.Id == orgId && o.OwnerId == requestingAuthorId, ct);
        if (org is null) return null;

        org.Name = newName;
        await db.SaveChangesAsync(ct);
        return org;
    }

    public async Task<OrganizationMemberEntity> AddMemberAsync(Guid orgId, byte[] authorId, string role, CancellationToken ct)
    {
        // Ensure author exists
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO authors (id, public_key) VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
            [authorId, authorId], ct);

        var existing = await db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == orgId && m.AuthorId == authorId, ct);

        if (existing is not null)
        {
            existing.Role = role;
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var member = new OrganizationMemberEntity
        {
            OrganizationId = orgId,
            AuthorId = authorId,
            Role = role,
            Joined = DateTimeOffset.UtcNow,
        };
        db.OrganizationMembers.Add(member);
        await db.SaveChangesAsync(ct);
        return member;
    }

    public async Task<bool> RemoveMemberAsync(Guid orgId, byte[] authorId, CancellationToken ct)
    {
        var member = await db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == orgId && m.AuthorId == authorId, ct);
        if (member is null) return false;

        db.OrganizationMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<OrganizationMemberEntity>> ListMembersAsync(Guid orgId, CancellationToken ct)
        => db.OrganizationMembers
            .Where(m => m.OrganizationId == orgId)
            .OrderBy(m => m.Joined)
            .ToListAsync(ct);

    public Task<OrganizationMemberEntity?> GetMemberAsync(Guid orgId, byte[] authorId, CancellationToken ct)
        => db.OrganizationMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == orgId && m.AuthorId == authorId, ct);
}
