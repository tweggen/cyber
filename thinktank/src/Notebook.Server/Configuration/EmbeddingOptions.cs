namespace Notebook.Server.Configuration;

public enum EmbeddingApiType
{
    Ollama,
    OpenAI,
}

public class EmbeddingOptions
{
    public EmbeddingApiType ApiType { get; set; } = EmbeddingApiType.Ollama;
    public string Url { get; set; } = "http://localhost:11434";
    public string? Token { get; set; }
    public string Model { get; set; } = "nomic-embed-text";
}
