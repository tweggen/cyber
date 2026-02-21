using System.Text;
using Cyber.Client.Filters;

namespace Cyber.Client.Tests;

public class HtmlContentFilterTests
{
    private readonly HtmlContentFilter _filter = new();

    private async Task<FilterResult> FilterHtml(string html)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        return await _filter.FilterAsync(stream, "test.html");
    }

    [Fact]
    public async Task Headings_ConvertToMarkdown()
    {
        var result = await FilterHtml("<h1>Title</h1><h2>Subtitle</h2><h3>Section</h3>");
        Assert.Contains("# Title", result.Text);
        Assert.Contains("## Subtitle", result.Text);
        Assert.Contains("### Section", result.Text);
    }

    [Fact]
    public async Task Paragraphs_SeparatedByBlankLines()
    {
        var result = await FilterHtml("<p>First paragraph</p><p>Second paragraph</p>");
        Assert.Contains("First paragraph", result.Text);
        Assert.Contains("Second paragraph", result.Text);
    }

    [Fact]
    public async Task Bold_ConvertToMarkdown()
    {
        var result = await FilterHtml("<p>This is <strong>bold</strong> text</p>");
        Assert.Contains("**bold**", result.Text);
    }

    [Fact]
    public async Task Italic_ConvertToMarkdown()
    {
        var result = await FilterHtml("<p>This is <em>italic</em> text</p>");
        Assert.Contains("*italic*", result.Text);
    }

    [Fact]
    public async Task Links_ConvertToMarkdown()
    {
        var result = await FilterHtml("<a href=\"https://example.com\">Example</a>");
        Assert.Contains("[Example](https://example.com)", result.Text);
    }

    [Fact]
    public async Task UnorderedList_ConvertToMarkdown()
    {
        var result = await FilterHtml("<ul><li>Item 1</li><li>Item 2</li></ul>");
        Assert.Contains("- Item 1", result.Text);
        Assert.Contains("- Item 2", result.Text);
    }

    [Fact]
    public async Task OrderedList_ConvertToMarkdown()
    {
        var result = await FilterHtml("<ol><li>First</li><li>Second</li></ol>");
        Assert.Contains("1. First", result.Text);
        Assert.Contains("2. Second", result.Text);
    }

    [Fact]
    public async Task CodeBlock_ConvertToMarkdown()
    {
        var result = await FilterHtml("<pre><code>var x = 1;</code></pre>");
        Assert.Contains("```", result.Text);
        Assert.Contains("var x = 1;", result.Text);
    }

    [Fact]
    public async Task InlineCode_ConvertToMarkdown()
    {
        var result = await FilterHtml("<p>Use <code>console.log</code> to debug</p>");
        Assert.Contains("`console.log`", result.Text);
    }

    [Fact]
    public async Task ScriptsAndStyles_AreStripped()
    {
        var result = await FilterHtml("""
            <html>
            <head><style>body { color: red; }</style></head>
            <body>
                <script>alert('xss')</script>
                <p>Clean content</p>
            </body>
            </html>
            """);
        Assert.Contains("Clean content", result.Text);
        Assert.DoesNotContain("alert", result.Text);
        Assert.DoesNotContain("color: red", result.Text);
    }

    [Fact]
    public async Task ContentType_IsMarkdown()
    {
        var result = await FilterHtml("<p>Hello</p>");
        Assert.Equal("text/markdown", result.ContentType);
    }

    [Fact]
    public async Task Table_ConvertToMarkdown()
    {
        var result = await FilterHtml("""
            <table>
                <tr><th>Name</th><th>Value</th></tr>
                <tr><td>A</td><td>1</td></tr>
            </table>
            """);
        Assert.Contains("| Name | Value |", result.Text);
        Assert.Contains("| --- | --- |", result.Text);
        Assert.Contains("| A | 1 |", result.Text);
    }

    [Fact]
    public async Task Blockquote_ConvertToMarkdown()
    {
        var result = await FilterHtml("<blockquote>Quoted text</blockquote>");
        Assert.Contains("> ", result.Text);
        Assert.Contains("Quoted text", result.Text);
    }
}
