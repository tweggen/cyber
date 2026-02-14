namespace Notebook.Core.Types;

using System.Text.Json.Serialization;

/// <summary>
/// A notebook entry combining v1 fields (content, crypto, causal context)
/// with v2 fields (claims, fragments, comparisons).
/// Class (not record) because EF Core change tracking requires mutable entities.
/// </summary>
public class Entry
{
    // ── v1 fields ──

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("notebook_id")]
    public Guid NotebookId { get; set; }

    [JsonPropertyName("content")]
    public byte[] Content { get; set; } = [];

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "text/plain";

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("author_id")]
    public byte[] AuthorId { get; set; } = [];

    [JsonPropertyName("signature")]
    public byte[] Signature { get; set; } = [];

    [JsonPropertyName("revision_of")]
    public Guid? RevisionOf { get; set; }

    [JsonPropertyName("references")]
    public List<Guid> References { get; set; } = [];

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }

    [JsonPropertyName("integration_cost")]
    public IntegrationCost? IntegrationCost { get; set; }

    // ── v2 fields ──

    /// <summary>Fixed-size claim representation extracted from content.</summary>
    [JsonPropertyName("claims")]
    public List<Claim> Claims { get; set; } = [];

    /// <summary>Processing status of the claims.</summary>
    [JsonPropertyName("claims_status")]
    public ClaimsStatus ClaimsStatus { get; set; } = ClaimsStatus.Pending;

    /// <summary>If this entry is a fragment of a larger artifact.</summary>
    [JsonPropertyName("fragment_of")]
    public Guid? FragmentOf { get; set; }

    /// <summary>Position in fragment chain (0-based).</summary>
    [JsonPropertyName("fragment_index")]
    public int? FragmentIndex { get; set; }

    /// <summary>Results of comparing this entry's claims against other entries.</summary>
    [JsonPropertyName("comparisons")]
    public List<ClaimComparison> Comparisons { get; set; } = [];

    /// <summary>Highest friction score across all comparisons.</summary>
    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; set; }

    /// <summary>True if max_friction exceeds the notebook's review threshold.</summary>
    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; set; }
}
