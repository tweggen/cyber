namespace Notebook.Core.Security;

public record SecurityLabel(ClassificationLevel Level, IReadOnlySet<string> Compartments)
{
    /// <summary>
    /// Returns true if this label dominates the other label.
    /// A label dominates another when its level is >= the other's level
    /// and its compartments are a superset of the other's compartments.
    /// </summary>
    public bool Dominates(SecurityLabel other) =>
        Level >= other.Level && other.Compartments.IsSubsetOf(Compartments);

    public static SecurityLabel Default { get; } =
        new(ClassificationLevel.Internal, new HashSet<string>());
}
