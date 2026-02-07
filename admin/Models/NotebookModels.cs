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
}

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
