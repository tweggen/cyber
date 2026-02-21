namespace Notebook.Core.Types;

public sealed record NeighborResult
{
    public Guid Id { get; init; }
    public List<Claim> Claims { get; init; } = [];
    public double Similarity { get; init; }
    public bool IsMirrored { get; init; }
    public Guid? SubscriptionId { get; init; }
    public double DiscountFactor { get; init; } = 1.0;
}
