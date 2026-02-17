using System.Text;
using Cyber.Client.Filters;

namespace Cyber.Client.Tests;

public class PlainTextFilterTests
{
    private readonly PlainTextFilter _filter = new();

    [Fact]
    public async Task Passthrough_PreservesContent()
    {
        var content = "Hello, world!\nSecond line.";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _filter.FilterAsync(stream, "test.txt");

        Assert.Equal(content, result.Text);
    }

    [Fact]
    public async Task ContentType_IsPlainText()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        var result = await _filter.FilterAsync(stream, "test.txt");

        Assert.Equal("text/plain", result.ContentType);
    }

    [Fact]
    public async Task EmptyFile_ReturnsEmptyString()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));

        var result = await _filter.FilterAsync(stream, "empty.txt");

        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task Utf8_WithBom_HandledCorrectly()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("BOM content")).ToArray();
        var stream = new MemoryStream(bytes);

        var result = await _filter.FilterAsync(stream, "bom.txt");

        Assert.Equal("BOM content", result.Text);
    }

    [Fact]
    public async Task MultilineContent_Preserved()
    {
        var content = "Line 1\nLine 2\nLine 3\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _filter.FilterAsync(stream, "multi.md");

        Assert.Equal(content, result.Text);
    }
}
