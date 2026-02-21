using Microsoft.EntityFrameworkCore;
using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public class AgentRepository(NotebookDbContext db) : IAgentRepository
{
    public async Task RegisterAsync(AgentEntity agent, CancellationToken ct)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AgentEntity?> GetAsync(string agentId, CancellationToken ct)
    {
        return await db.Agents.FindAsync([agentId], ct);
    }

    public async Task<List<AgentEntity>> ListAsync(CancellationToken ct)
    {
        return await db.Agents.AsNoTracking()
            .OrderByDescending(a => a.Registered)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(AgentEntity agent, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string agentId, CancellationToken ct)
    {
        var entity = await db.Agents.FindAsync([agentId], ct);
        if (entity is null)
            return false;

        db.Agents.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task TouchLastSeenAsync(string agentId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE agents SET last_seen = NOW() WHERE id = {0}",
            [agentId],
            ct);
    }
}
