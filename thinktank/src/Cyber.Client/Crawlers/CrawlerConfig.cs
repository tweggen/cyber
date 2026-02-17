using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyber.Client.Crawlers;

public sealed record CrawlerConfig
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("schedule")]
    public string? Schedule { get; init; }

    [JsonPropertyName("settings")]
    public Dictionary<string, JsonElement>? Settings { get; init; }
}
