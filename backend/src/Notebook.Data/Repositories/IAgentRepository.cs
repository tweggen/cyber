using Notebook.Data.Entities;

namespace Notebook.Data.Repositories;

public interface IAgentRepository
{
    Task RegisterAsync(AgentEntity agent, CancellationToken ct);
    Task<AgentEntity?> GetAsync(string agentId, CancellationToken ct);
    Task<List<AgentEntity>> ListAsync(CancellationToken ct);
    Task UpdateAsync(AgentEntity agent, CancellationToken ct);
    Task<bool> DeleteAsync(string agentId, CancellationToken ct);
    Task TouchLastSeenAsync(string agentId, CancellationToken ct);
}
