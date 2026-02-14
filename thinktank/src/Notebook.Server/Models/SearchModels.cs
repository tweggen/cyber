using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record SearchResponse
{
    [JsonPropertyName("results")]
    public required List<SearchResult> Results { get; init; }
}
