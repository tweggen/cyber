using Notebook.Server.Services;

namespace Notebook.Tests.Services;

public class ContentNormalizerTests
{
    private readonly ContentNormalizer _normalizer = new();

    [Fact]
    public void PlainText_PassesThrough()
    {
        var result = _normalizer.Normalize("Hello world", "text/plain");

        Assert.Equal("Hello world", result.Content);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Null(result.OriginalContentType);
    }

    [Fact]
    public void Markdown_PassesThrough()
    {
        var md = "# Title\n\nSome paragraph.";
        var result = _normalizer.Normalize(md, "text/markdown");

        Assert.Equal(md, result.Content);
        Assert.Equal("text/markdown", result.ContentType);
        Assert.Null(result.OriginalContentType);
    }

    [Fact]
    public void Html_ConvertsToMarkdown()
    {
        var html = "<html><body><h1>Title</h1><p>Paragraph text.</p></body></html>";
        var result = _normalizer.Normalize(html, "text/html");

        Assert.Equal("text/markdown", result.ContentType);
        Assert.Equal("text/html", result.OriginalContentType);
        Assert.Contains("# Title", result.Content);
        Assert.Contains("Paragraph text.", result.Content);
    }

    [Fact]
    public void Html_RemovesScriptsAndStyles()
    {
        var html = """
            <html><body>
                <script>alert('xss')</script>
                <style>.red { color: red; }</style>
                <p>Clean content.</p>
            </body></html>
            """;
        var result = _normalizer.Normalize(html, "text/html");

        Assert.DoesNotContain("alert", result.Content);
        Assert.DoesNotContain(".red", result.Content);
        Assert.Contains("Clean content.", result.Content);
    }

    [Fact]
    public void Html_ConvertsLinks()
    {
        var html = """<html><body><p><a href="https://example.com">Click here</a></p></body></html>""";
        var result = _normalizer.Normalize(html, "text/html");

        Assert.Contains("[Click here](https://example.com)", result.Content);
    }

    [Fact]
    public void Html_ConvertsLists()
    {
        var html = """
            <html><body>
                <ul><li>Item 1</li><li>Item 2</li></ul>
            </body></html>
            """;
        var result = _normalizer.Normalize(html, "text/html");

        Assert.Contains("- Item 1", result.Content);
        Assert.Contains("- Item 2", result.Content);
    }

    [Fact]
    public void Html_ConvertsCodeBlocks()
    {
        var html = """<html><body><pre><code>var x = 1;</code></pre></body></html>""";
        var result = _normalizer.Normalize(html, "text/html");

        Assert.Contains("```", result.Content);
        Assert.Contains("var x = 1;", result.Content);
    }

    [Fact]
    public void Html_CaseInsensitiveContentType()
    {
        var html = "<html><body><p>Hello</p></body></html>";
        var result = _normalizer.Normalize(html, "TEXT/HTML");

        Assert.Equal("text/markdown", result.ContentType);
        Assert.Equal("text/html", result.OriginalContentType);
    }

    [Fact]
    public void UnknownContentType_PassesThrough()
    {
        var result = _normalizer.Normalize("binary data", "application/octet-stream");

        Assert.Equal("binary data", result.Content);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Null(result.OriginalContentType);
    }
}
