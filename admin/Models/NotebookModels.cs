using System.Text.Json.Serialization;

namespace NotebookAdmin.Models;

/// <summary>
/// DTO for notebook summary from Rust API.
/// </summary>
public class NotebookSummary
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("is_owner")]
    public bool IsOwner { get; set; }

    [JsonPropertyName("permissions")]
    public NotebookPermissions Permissions { get; set; } = new();

    [JsonPropertyName("total_entries")]
    public long TotalEntries { get; set; }

    [JsonPropertyName("total_entropy")]
    public double TotalEntropy { get; set; }

    [JsonPropertyName("last_activity_sequence")]
    public long LastActivitySequence { get; set; }

    [JsonPropertyName("participant_count")]
    public long ParticipantCount { get; set; }
}

public class NotebookPermissions
{
    [JsonPropertyName("read")]
    public bool Read { get; set; }

    [JsonPropertyName("write")]
    public bool Write { get; set; }
}

public class ListNotebooksResponse
{
    [JsonPropertyName("notebooks")]
    public List<NotebookSummary> Notebooks { get; set; } = [];
}

/// <summary>
/// DTO for creating a notebook via Rust API.
/// </summary>
public class CreateNotebookRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class CreateNotebookResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }
}

/// <summary>
/// DTO for creating an entry via Rust API.
/// </summary>
public class CreateEntryRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = "text/plain";

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("references")]
    public List<Guid> References { get; set; } = [];
}

public class CreateEntryResponse
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; set; }

    [JsonPropertyName("causal_position")]
    public CausalPosition CausalPosition { get; set; } = new();

    [JsonPropertyName("integration_cost")]
    public IntegrationCost IntegrationCost { get; set; } = new();
}

public class CausalPosition
{
    [JsonPropertyName("sequence")]
    public ulong Sequence { get; set; }
}

public class IntegrationCost
{
    [JsonPropertyName("entries_revised")]
    public uint EntriesRevised { get; set; }

    [JsonPropertyName("references_broken")]
    public uint ReferencesBroken { get; set; }

    [JsonPropertyName("catalog_shift")]
    public double CatalogShift { get; set; }

    [JsonPropertyName("orphan")]
    public bool Orphan { get; set; }
}

/// <summary>
/// DTO for browse response from Rust API.
/// </summary>
public class BrowseResponse
{
    [JsonPropertyName("catalog")]
    public List<ClusterSummary> Catalog { get; set; } = [];

    [JsonPropertyName("notebook_entropy")]
    public double NotebookEntropy { get; set; }

    [JsonPropertyName("total_entries")]
    public uint TotalEntries { get; set; }
}

public class ClusterSummary
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("entry_count")]
    public uint EntryCount { get; set; }

    [JsonPropertyName("cumulative_cost")]
    public double CumulativeCost { get; set; }

    [JsonPropertyName("stability")]
    public ulong Stability { get; set; }

    [JsonPropertyName("representative_entry_ids")]
    public List<Guid> RepresentativeEntryIds { get; set; } = [];
}

/// <summary>
/// DTO for observe response from Rust API.
/// </summary>
public class ObserveResponse
{
    [JsonPropertyName("changes")]
    public List<ChangeEntry> Changes { get; set; } = [];

    [JsonPropertyName("notebook_entropy")]
    public double NotebookEntropy { get; set; }

    [JsonPropertyName("current_sequence")]
    public ulong CurrentSequence { get; set; }
}

public class ChangeEntry
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; set; }

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("integration_cost")]
    public IntegrationCost IntegrationCost { get; set; } = new();

    [JsonPropertyName("causal_position")]
    public CausalPosition CausalPosition { get; set; } = new();

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("integration_status")]
    public string? IntegrationStatus { get; set; }
}

// ============================================================================
// Revise Entry DTOs
// ============================================================================

/// <summary>
/// DTO for revising an entry via Rust API.
/// </summary>
public class ReviseEntryRequest
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public class ReviseEntryResponse
{
    [JsonPropertyName("revision_id")]
    public Guid RevisionId { get; set; }

    [JsonPropertyName("causal_position")]
    public CausalPosition CausalPosition { get; set; } = new();

    [JsonPropertyName("integration_cost")]
    public IntegrationCost IntegrationCost { get; set; } = new();
}

// ============================================================================
// Read Entry DTOs
// ============================================================================

/// <summary>
/// Full response from GET /notebooks/{id}/entries/{entryId}.
/// </summary>
public class ReadEntryResponse
{
    [JsonPropertyName("entry")]
    public EntryDetail Entry { get; set; } = new();

    [JsonPropertyName("revisions")]
    public List<EntrySummaryDto> Revisions { get; set; } = [];

    [JsonPropertyName("references")]
    public List<EntrySummaryDto> References { get; set; } = [];

    [JsonPropertyName("referenced_by")]
    public List<EntrySummaryDto> ReferencedBy { get; set; } = [];

    [JsonPropertyName("fragments")]
    public List<FragmentSummaryDto> Fragments { get; set; } = [];
}

/// <summary>
/// Full entry detail for read response.
/// </summary>
public class EntryDetail
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("content")]
    public object? Content { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("references")]
    public List<Guid> References { get; set; } = [];

    [JsonPropertyName("revision_of")]
    public Guid? RevisionOf { get; set; }

    [JsonPropertyName("causal_position")]
    public CausalPositionDto CausalPosition { get; set; } = new();

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("integration_cost")]
    public IntegrationCost IntegrationCost { get; set; } = new();

    [JsonPropertyName("claims")]
    public List<ClaimDto> Claims { get; set; } = [];

    [JsonPropertyName("claims_status")]
    public string? ClaimsStatus { get; set; }

    [JsonPropertyName("comparisons")]
    public List<ComparisonDto> Comparisons { get; set; } = [];

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; set; }

    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; set; }

    [JsonPropertyName("fragment_of")]
    public Guid? FragmentOf { get; set; }

    [JsonPropertyName("fragment_index")]
    public int? FragmentIndex { get; set; }

    [JsonPropertyName("integration_status")]
    public string? IntegrationStatus { get; set; }
}

/// <summary>
/// Causal position with activity context.
/// </summary>
public class CausalPositionDto
{
    [JsonPropertyName("sequence")]
    public ulong Sequence { get; set; }

    [JsonPropertyName("activity_context")]
    public ActivityContextDto ActivityContext { get; set; } = new();
}

/// <summary>
/// Activity context at entry creation time.
/// </summary>
public class ActivityContextDto
{
    [JsonPropertyName("entries_since_last_by_author")]
    public uint EntriesSinceLastByAuthor { get; set; }

    [JsonPropertyName("total_notebook_entries")]
    public uint TotalNotebookEntries { get; set; }

    [JsonPropertyName("recent_entropy")]
    public double RecentEntropy { get; set; }
}

/// <summary>
/// Summary of an entry in references/revisions lists.
/// </summary>
public class EntrySummaryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }
}

// ============================================================================
// Claims & Comparisons DTOs
// ============================================================================

/// <summary>
/// A factual claim extracted from an entry's content.
/// </summary>
public class ClaimDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

/// <summary>
/// Result of comparing two entries' claim-sets.
/// </summary>
public class ComparisonDto
{
    [JsonPropertyName("compared_against")]
    public Guid ComparedAgainst { get; set; }

    [JsonPropertyName("entropy")]
    public double Entropy { get; set; }

    [JsonPropertyName("friction")]
    public double Friction { get; set; }

    [JsonPropertyName("contradictions")]
    public List<ContradictionDto> Contradictions { get; set; } = [];

    [JsonPropertyName("computed_at")]
    public DateTime ComputedAt { get; set; }
}

/// <summary>
/// Summary of a fragment entry with its claims.
/// </summary>
public class FragmentSummaryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("fragment_index")]
    public int FragmentIndex { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("claims")]
    public List<ClaimDto> Claims { get; set; } = [];

    [JsonPropertyName("claims_status")]
    public string? ClaimsStatus { get; set; }
}

/// <summary>
/// A specific contradiction between two claims.
/// </summary>
public class ContradictionDto
{
    [JsonPropertyName("claim_a")]
    public string ClaimA { get; set; } = string.Empty;

    [JsonPropertyName("claim_b")]
    public string ClaimB { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public double Severity { get; set; }
}

// ============================================================================
// Share DTOs
// ============================================================================

/// <summary>
/// Request body for sharing a notebook.
/// </summary>
public class ShareRequest
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public SharePermissions Permissions { get; set; } = new();
}

public class SharePermissions
{
    [JsonPropertyName("read")]
    public bool Read { get; set; }

    [JsonPropertyName("write")]
    public bool Write { get; set; }
}

public class ShareResponse
{
    [JsonPropertyName("access_granted")]
    public bool AccessGranted { get; set; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public SharePermissions Permissions { get; set; } = new();
}

public class RevokeResponse
{
    [JsonPropertyName("access_revoked")]
    public bool AccessRevoked { get; set; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;
}

/// <summary>
/// Response for listing notebook participants.
/// </summary>
public class ParticipantsResponse
{
    [JsonPropertyName("participants")]
    public List<ParticipantDto> Participants { get; set; } = [];
}

public class ParticipantDto
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public SharePermissions Permissions { get; set; } = new();

    [JsonPropertyName("granted_at")]
    public DateTime GrantedAt { get; set; }
}

/// <summary>
/// DTO for renaming a notebook via Rust API.
/// </summary>
public class RenameNotebookRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class RenameNotebookResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Response for deleting a notebook.
/// </summary>
public class DeleteNotebookResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

// ============================================================================
// Author Registration DTOs
// ============================================================================

/// <summary>
/// DTO for author registration via Rust API.
/// </summary>
public class RegisterAuthorRequest
{
    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = string.Empty;
}

public class RegisterAuthorResponse
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;
}

/// <summary>
/// DTO for user registration request.
/// </summary>
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

/// <summary>
/// DTO for token request.
/// </summary>
public class TokenRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO for token response.
/// </summary>
public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }
}
