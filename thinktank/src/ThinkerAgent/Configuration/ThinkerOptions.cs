using System.Text.Json.Serialization;

namespace ThinkerAgent.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApiType
{
    Ollama,
    OpenAi,
}

public sealed class ThinkerOptions
{
    public const string SectionName = "Thinker";

    public string ServerUrl { get; set; } = "http://localhost:5000";
    public Guid NotebookId { get; set; }
    public string Token { get; set; } = "";
    public int WorkerCount { get; set; } = 1;
    public ApiType ApiType { get; set; } = ApiType.Ollama;
    public string LlmUrl { get; set; } = "http://localhost:11434";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemma3:12b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public double PollIntervalSeconds { get; set; } = 5.0;
    public List<string>? JobTypes { get; set; }
}
