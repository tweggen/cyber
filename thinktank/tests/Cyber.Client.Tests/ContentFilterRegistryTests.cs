using Cyber.Client.Filters;

namespace Cyber.Client.Tests;

public class ContentFilterRegistryTests
{
    private readonly ContentFilterRegistry _registry = new();

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void HtmlExtensions_ReturnHtmlFilter(string ext)
    {
        var filter = _registry.GetFilter($"file{ext}");
        Assert.NotNull(filter);
        Assert.IsType<HtmlContentFilter>(filter);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".markdown")]
    public void TextExtensions_ReturnPlainTextFilter(string ext)
    {
        var filter = _registry.GetFilter($"file{ext}");
        Assert.NotNull(filter);
        Assert.IsType<PlainTextFilter>(filter);
    }

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".jpg")]
    [InlineData("")]
    public void UnsupportedExtensions_ReturnNull(string ext)
    {
        var filter = _registry.GetFilter($"file{ext}");
        Assert.Null(filter);
    }

    [Fact]
    public void IsSupported_ReturnsTrueForKnownExtensions()
    {
        Assert.True(_registry.IsSupported("test.html"));
        Assert.True(_registry.IsSupported("test.txt"));
    }

    [Fact]
    public void IsSupported_ReturnsFalseForUnknownExtensions()
    {
        Assert.False(_registry.IsSupported("test.pdf"));
        Assert.False(_registry.IsSupported("noext"));
    }

    [Fact]
    public void CaseInsensitive_ExtensionLookup()
    {
        Assert.NotNull(_registry.GetFilter("FILE.HTML"));
        Assert.NotNull(_registry.GetFilter("file.TXT"));
    }

    [Fact]
    public void Register_CustomFilter_Works()
    {
        var customFilter = new PlainTextFilter();
        _registry.Register(".csv", customFilter);
        Assert.Same(customFilter, _registry.GetFilter("data.csv"));
    }

    [Fact]
    public void SupportedExtensions_ContainsExpected()
    {
        var extensions = _registry.SupportedExtensions;
        Assert.Contains(".html", extensions);
        Assert.Contains(".htm", extensions);
        Assert.Contains(".txt", extensions);
        Assert.Contains(".md", extensions);
    }
}
