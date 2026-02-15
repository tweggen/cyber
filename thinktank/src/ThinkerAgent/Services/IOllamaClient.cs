namespace ThinkerAgent.Services;

public sealed record OllamaModel(string Name, string ModifiedAt, long Size);

public sealed record OllamaChatResponse(string Content, double? TokensPerSecond);

public interface IOllamaClient
{
    Task<bool> IsRunningAsync(CancellationToken ct = default);
    Task<List<OllamaModel>> ListModelsAsync(CancellationToken ct = default);
    Task<OllamaChatResponse> ChatAsync(string model, string prompt, int maxTokens, CancellationToken ct = default);
}
