namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// A single factual claim extracted from an entry's content.
/// Claims are the fixed-size representation used for comparison,
/// navigation, and indexing.
/// </summary>
public sealed record Claim
{
    /// <summary>Short declarative statement (1-2 sentences).</summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>How central this claim is to the entry (0.0 to 1.0).</summary>
    [JsonPropertyName("confidence")]
    public required double Confidence { get; init; }
}

/// <summary>Processing status of an entry's claims.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClaimsStatus>))]
public enum ClaimsStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("distilled")]
    Distilled,

    [JsonStringEnumMemberName("verified")]
    Verified,
}
