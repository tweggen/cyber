using System.Text.Json.Serialization;
using Notebook.Core.Types;

namespace Notebook.Server.Models;

public sealed record UpdateClaimsRequest
{
    [JsonPropertyName("claims")]
    public required List<Claim> Claims { get; init; }

    /// <summary>Identifier of the worker that produced these claims.</summary>
    [JsonPropertyName("author")]
    public required string Author { get; init; }
}

public sealed record UpdateClaimsResponse
{
    [JsonPropertyName("entry_id")]
    public required Guid EntryId { get; init; }

    [JsonPropertyName("claims_status")]
    public required ClaimsStatus ClaimsStatus { get; init; }

    [JsonPropertyName("comparison_jobs_created")]
    public required int ComparisonJobsCreated { get; init; }
}
