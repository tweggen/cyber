namespace ThinkerAgent.Configuration;

public sealed class ThinkerOptions
{
    public const string SectionName = "Thinker";

    public string ServerUrl { get; set; } = "http://localhost:5000";
    public Guid NotebookId { get; set; }
    public string Token { get; set; } = "";
    public int WorkerCount { get; set; } = 1;
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public double PollIntervalSeconds { get; set; } = 5.0;
    public List<string>? JobTypes { get; set; }
}
