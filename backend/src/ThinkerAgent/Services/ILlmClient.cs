namespace ThinkerAgent.Services;

public sealed record LlmModel(string Name, string ModifiedAt, long Size);

public sealed record LlmChatResponse(string Content, double? TokensPerSecond);

public sealed record LlmEmbedResponse(double[][] Embeddings);

public interface ILlmClient
{
    Task<bool> IsRunningAsync(CancellationToken ct = default);
    Task<List<LlmModel>> ListModelsAsync(CancellationToken ct = default);
    Task<LlmChatResponse> ChatAsync(string model, string prompt, int maxTokens,
        IProgress<int>? tokenProgress = null, CancellationToken ct = default);
    Task<LlmEmbedResponse> EmbedAsync(string model, List<string> input, CancellationToken ct = default);
}
