using Notebook.Core.Security;

namespace Notebook.Tests.Security;

public class SecurityLabelTests
{
    [Fact]
    public void EqualLabelsDominate()
    {
        var a = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "ALPHA" });
        var b = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "ALPHA" });
        Assert.True(a.Dominates(b));
        Assert.True(b.Dominates(a));
    }

    [Fact]
    public void HigherLevelDominatesLower()
    {
        var high = new SecurityLabel(ClassificationLevel.TopSecret, new HashSet<string>());
        var low = new SecurityLabel(ClassificationLevel.Internal, new HashSet<string>());
        Assert.True(high.Dominates(low));
        Assert.False(low.Dominates(high));
    }

    [Fact]
    public void SupersetCompartmentsDominate()
    {
        var broad = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "ALPHA", "BETA" });
        var narrow = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "ALPHA" });
        Assert.True(broad.Dominates(narrow));
        Assert.False(narrow.Dominates(broad));
    }

    [Fact]
    public void DisjointCompartmentsDoNotDominate()
    {
        var a = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "ALPHA" });
        var b = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string> { "BETA" });
        Assert.False(a.Dominates(b));
        Assert.False(b.Dominates(a));
    }

    [Fact]
    public void HigherLevelButMissingCompartmentDoesNotDominate()
    {
        var high = new SecurityLabel(ClassificationLevel.TopSecret, new HashSet<string>());
        var low = new SecurityLabel(ClassificationLevel.Internal, new HashSet<string> { "ALPHA" });
        Assert.False(high.Dominates(low));
    }

    [Fact]
    public void DefaultLabelDominatesPublic()
    {
        var pub = new SecurityLabel(ClassificationLevel.Public, new HashSet<string>());
        Assert.True(SecurityLabel.Default.Dominates(pub));
    }

    [Fact]
    public void DefaultLabelDoesNotDominateSecret()
    {
        var secret = new SecurityLabel(ClassificationLevel.Secret, new HashSet<string>());
        Assert.False(SecurityLabel.Default.Dominates(secret));
    }
}
