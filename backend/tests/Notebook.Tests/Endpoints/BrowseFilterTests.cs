using Notebook.Core.Types;

namespace Notebook.Tests.Endpoints;

public class BrowseFilterTests
{
    [Fact]
    public void BrowseFilter_NoFilters_HasFiltersIsFalse()
    {
        var f = new BrowseFilter();
        Assert.False(f.HasFilters);
    }

    [Fact]
    public void BrowseFilter_WithTopicPrefix_HasFiltersIsTrue()
    {
        var f = new BrowseFilter { TopicPrefix = "confluence/ENG/" };
        Assert.True(f.HasFilters);
    }

    [Fact]
    public void BrowseFilter_WithMultipleFilters()
    {
        var f = new BrowseFilter
        {
            TopicPrefix = "confluence/ENG/",
            ClaimsStatus = "pending",
            Limit = 20,
        };
        Assert.True(f.HasFilters);
    }

    [Fact]
    public void BrowseFilter_QueryOnly_HasFiltersIsFalse()
    {
        // Query alone does not trigger filtered mode (it's the existing param)
        var f = new BrowseFilter { Query = "test" };
        Assert.False(f.HasFilters);
    }

    [Fact]
    public void BrowseFilter_WithNeedsReview_HasFiltersIsTrue()
    {
        var f = new BrowseFilter { NeedsReview = true };
        Assert.True(f.HasFilters);
    }

    [Fact]
    public void BrowseFilter_WithFrictionThreshold_HasFiltersIsTrue()
    {
        var f = new BrowseFilter { HasFrictionAbove = 0.3 };
        Assert.True(f.HasFilters);
    }
}
