namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// Result of comparing two claim-sets for entropy (novelty) and friction (contradiction).
/// </summary>
public sealed record ClaimComparison
{
    /// <summary>The entry this was compared against.</summary>
    [JsonPropertyName("compared_against")]
    public Guid? ComparedAgainst { get; init; }

    /// <summary>Novelty score: fraction of claims covering new ground (0.0 to 1.0).</summary>
    [JsonPropertyName("entropy")]
    public double Entropy { get; init; }

    /// <summary>Contradiction score: fraction of claims that contradict existing claims (0.0 to 1.0).</summary>
    [JsonPropertyName("friction")]
    public double Friction { get; init; }

    /// <summary>Details for each contradiction found.</summary>
    [JsonPropertyName("contradictions")]
    public List<Contradiction> Contradictions { get; init; } = [];

    /// <summary>When this comparison was computed.</summary>
    [JsonPropertyName("computed_at")]
    public DateTimeOffset? ComputedAt { get; init; }

    /// <summary>Which robot worker computed this.</summary>
    [JsonPropertyName("computed_by")]
    public string? ComputedBy { get; init; }
}

/// <summary>A specific contradiction between two claims.</summary>
public sealed record Contradiction
{
    /// <summary>The existing claim that is contradicted.</summary>
    [JsonPropertyName("claim_a")]
    public required string ClaimA { get; init; }

    /// <summary>The new claim that contradicts it.</summary>
    [JsonPropertyName("claim_b")]
    public required string ClaimB { get; init; }

    /// <summary>How directly they contradict (0.0 to 1.0).</summary>
    [JsonPropertyName("severity")]
    public required double Severity { get; init; }
}
