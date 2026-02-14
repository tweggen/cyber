namespace Notebook.Core.Types;

/// <summary>
/// Filter parameters for browsing entries. Used by the repository layer.
/// </summary>
public sealed record BrowseFilter
{
    public string? Query { get; init; }
    public int? MaxEntries { get; init; }
    public string? TopicPrefix { get; init; }
    public string? ClaimsStatus { get; init; }
    public string? Author { get; init; }
    public long? SequenceMin { get; init; }
    public long? SequenceMax { get; init; }
    public Guid? FragmentOf { get; init; }
    public double? HasFrictionAbove { get; init; }
    public bool? NeedsReview { get; init; }
    public int? Limit { get; init; }
    public int? Offset { get; init; }

    /// <summary>Returns true if any v2 filter parameter is set.</summary>
    public bool HasFilters =>
        TopicPrefix is not null || ClaimsStatus is not null || Author is not null ||
        SequenceMin is not null || SequenceMax is not null || FragmentOf is not null ||
        HasFrictionAbove is not null || NeedsReview is not null ||
        Limit is not null || Offset is not null;
}
