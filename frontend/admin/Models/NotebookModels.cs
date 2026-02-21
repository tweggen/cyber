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
/// DTO for filtered browse response from .NET API.
/// </summary>
public class BrowseFilteredResponse
{
    [JsonPropertyName("entries")]
    public List<BrowseEntryDto> Entries { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Single entry from filtered browse.
/// </summary>
public class BrowseEntryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("claims_status")]
    public string ClaimsStatus { get; set; } = "pending";

    [JsonPropertyName("max_friction")]
    public double? MaxFriction { get; set; }

    [JsonPropertyName("needs_review")]
    public bool NeedsReview { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("claim_count")]
    public int ClaimCount { get; set; }

    [JsonPropertyName("integration_status")]
    public string IntegrationStatus { get; set; } = "probation";
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

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "read";
}

public class SharePermissions
{
    [JsonPropertyName("read")]
    public bool Read { get; set; }

    [JsonPropertyName("write")]
    public bool Write { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }
}

public class ShareResponse
{
    [JsonPropertyName("notebook_id")]
    public Guid NotebookId { get; set; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = string.Empty;

    [JsonPropertyName("granted")]
    public bool Granted { get; set; }
}

public class RevokeResponse
{
    [JsonPropertyName("notebook_id")]
    public Guid NotebookId { get; set; }

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
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

// ============================================================================
// Organization & Group DTOs
// ============================================================================

public class CreateOrganizationRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class OrganizationResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }
}

public class ListOrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public List<OrganizationResponse> Organizations { get; set; } = [];
}

public class CreateGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parent_id")]
    public Guid? ParentId { get; set; }
}

public class GroupResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }
}

public class ListGroupsResponse
{
    [JsonPropertyName("groups")]
    public List<GroupResponse> Groups { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<EdgeResponse> Edges { get; set; } = [];
}

public class EdgeResponse
{
    [JsonPropertyName("parent_id")]
    public Guid ParentId { get; set; }

    [JsonPropertyName("child_id")]
    public Guid ChildId { get; set; }
}

public class AddEdgeRequest
{
    [JsonPropertyName("parent_id")]
    public Guid ParentId { get; set; }

    [JsonPropertyName("child_id")]
    public Guid ChildId { get; set; }
}

public class AddMemberRequest
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";
}

public class MemberResponse
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("granted")]
    public DateTimeOffset Granted { get; set; }

    [JsonPropertyName("granted_by")]
    public string? GrantedBy { get; set; }
}

public class ListMembersResponse
{
    [JsonPropertyName("members")]
    public List<MemberResponse> Members { get; set; } = [];
}

public class AssignGroupRequest
{
    [JsonPropertyName("group_id")]
    public Guid? GroupId { get; set; }
}

// ============================================================================
// Clearance DTOs
// ============================================================================

public class GrantClearanceRequest
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("max_level")]
    public string MaxLevel { get; set; } = "INTERNAL";

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; set; } = [];
}

public class ClearanceSummaryResponse
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("max_level")]
    public string MaxLevel { get; set; } = string.Empty;

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; set; } = [];

    [JsonPropertyName("granted")]
    public DateTimeOffset Granted { get; set; }
}

public class ListClearancesResponse
{
    [JsonPropertyName("clearances")]
    public List<ClearanceSummaryResponse> Clearances { get; set; } = [];
}

// ============================================================================
// Review DTOs
// ============================================================================

public class ReviewItemResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("notebook_id")]
    public Guid NotebookId { get; set; }

    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; set; }

    [JsonPropertyName("submitter")]
    public string Submitter { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("reviewer")]
    public string? Reviewer { get; set; }

    [JsonPropertyName("reviewed_at")]
    public DateTimeOffset? ReviewedAt { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }
}

public class ListReviewsResponse
{
    [JsonPropertyName("reviews")]
    public List<ReviewItemResponse> Reviews { get; set; } = [];

    [JsonPropertyName("pending_count")]
    public int PendingCount { get; set; }
}

// ============================================================================
// Agent DTOs
// ============================================================================

public class RegisterAgentRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("max_level")]
    public string MaxLevel { get; set; } = "INTERNAL";

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; set; } = [];

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; set; }
}

public class UpdateAgentRequest
{
    [JsonPropertyName("max_level")]
    public string MaxLevel { get; set; } = "INTERNAL";

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; set; } = [];

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; set; }
}

public class AgentResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("organization_id")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("max_level")]
    public string MaxLevel { get; set; } = string.Empty;

    [JsonPropertyName("compartments")]
    public List<string> Compartments { get; set; } = [];

    [JsonPropertyName("infrastructure")]
    public string? Infrastructure { get; set; }

    [JsonPropertyName("registered")]
    public DateTimeOffset Registered { get; set; }

    [JsonPropertyName("last_seen")]
    public DateTimeOffset? LastSeen { get; set; }
}

public class ListAgentsResponse
{
    [JsonPropertyName("agents")]
    public List<AgentResponse> Agents { get; set; } = [];
}

// ============================================================================
// Subscription DTOs
// ============================================================================

public class CreateSubscriptionRequest
{
    [JsonPropertyName("source_id")]
    public Guid SourceId { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "catalog";

    [JsonPropertyName("topic_filter")]
    public string? TopicFilter { get; set; }

    [JsonPropertyName("discount_factor")]
    public double DiscountFactor { get; set; } = 0.3;

    [JsonPropertyName("poll_interval_s")]
    public int PollIntervalSeconds { get; set; } = 60;
}

public class SubscriptionResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("subscriber_id")]
    public Guid SubscriberId { get; set; }

    [JsonPropertyName("source_id")]
    public Guid SourceId { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("topic_filter")]
    public string? TopicFilter { get; set; }

    [JsonPropertyName("sync_status")]
    public string SyncStatus { get; set; } = string.Empty;

    [JsonPropertyName("sync_watermark")]
    public long SyncWatermark { get; set; }

    [JsonPropertyName("last_sync_at")]
    public DateTimeOffset? LastSyncAt { get; set; }

    [JsonPropertyName("sync_error")]
    public string? SyncError { get; set; }

    [JsonPropertyName("mirrored_count")]
    public int MirroredCount { get; set; }

    [JsonPropertyName("discount_factor")]
    public double DiscountFactor { get; set; }

    [JsonPropertyName("poll_interval_s")]
    public int PollIntervalSeconds { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }
}

public class ListSubscriptionsResponse
{
    [JsonPropertyName("subscriptions")]
    public List<SubscriptionResponse> Subscriptions { get; set; } = [];
}

// ============================================================================
// Audit DTOs
// ============================================================================

public class AuditLogEntryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("notebook_id")]
    public Guid? NotebookId { get; set; }

    [JsonPropertyName("author_id")]
    public string? AuthorId { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("target_type")]
    public string? TargetType { get; set; }

    [JsonPropertyName("target_id")]
    public string? TargetId { get; set; }

    [JsonPropertyName("detail")]
    public System.Text.Json.JsonElement? Detail { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }
}

public class AuditResponseDto
{
    [JsonPropertyName("entries")]
    public List<AuditLogEntryDto> Entries { get; set; } = [];
}

// ============================================================================
// Search DTOs
// ============================================================================

public class SearchResultDto
{
    [JsonPropertyName("entry_id")]
    public Guid EntryId { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("snippet")]
    public string Snippet { get; set; } = string.Empty;

    [JsonPropertyName("match_location")]
    public string MatchLocation { get; set; } = string.Empty;

    [JsonPropertyName("relevance_score")]
    public double RelevanceScore { get; set; }
}

public class LexicalSearchResponse
{
    [JsonPropertyName("results")]
    public List<SearchResultDto> Results { get; set; } = [];
}

// ============================================================================
// Job Stats DTOs
// ============================================================================

public class JobTypeStats
{
    [JsonPropertyName("pending")]
    public long Pending { get; set; }

    [JsonPropertyName("in_progress")]
    public long InProgress { get; set; }

    [JsonPropertyName("completed")]
    public long Completed { get; set; }

    [JsonPropertyName("failed")]
    public long Failed { get; set; }
}

public class JobStatsResponse
{
    [JsonPropertyName("DISTILL_CLAIMS")]
    public JobTypeStats DistillClaims { get; set; } = new();

    [JsonPropertyName("COMPARE_CLAIMS")]
    public JobTypeStats CompareClaims { get; set; } = new();

    [JsonPropertyName("CLASSIFY_TOPIC")]
    public JobTypeStats ClassifyTopic { get; set; } = new();

    [JsonPropertyName("EMBED_CLAIMS")]
    public JobTypeStats EmbedClaims { get; set; } = new();
}
