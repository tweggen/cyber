using Notebook.Server.Services;

namespace Notebook.Tests.Services;

public class MarkdownFragmenterTests
{
    private readonly MarkdownFragmenter _fragmenter = new();

    [Fact]
    public void SmallContent_NotFragmented()
    {
        var md = "# Title\n\nShort paragraph.";
        var fragments = _fragmenter.Fragment(md);

        Assert.Empty(fragments);
    }

    [Fact]
    public void ContentAtBudget_NotFragmented()
    {
        // 4000 tokens * 4 chars = 16000 chars
        var md = new string('x', 16000);
        var fragments = _fragmenter.Fragment(md);

        Assert.Empty(fragments);
    }

    [Fact]
    public void LargeContentWithHeadings_SplitsAtHeadings()
    {
        // Create content with two large sections, each ~10K chars
        var section1 = "# Section One\n\n" + new string('a', 10000) + "\n\n";
        var section2 = "# Section Two\n\n" + new string('b', 10000) + "\n\n";
        var md = section1 + section2;

        var fragments = _fragmenter.Fragment(md);

        Assert.True(fragments.Count >= 2);
        Assert.Contains("Section One", fragments[0].Content);
        Assert.Contains("Section Two", fragments[^1].Content);

        // Fragment indices should be sequential starting at 0
        for (var i = 0; i < fragments.Count; i++)
            Assert.Equal(i, fragments[i].Index);
    }

    [Fact]
    public void LargeContentWithoutHeadings_SplitsAtParagraphs()
    {
        // Create content with many paragraphs, total > 16000 chars
        var paragraphs = string.Join("\n\n", Enumerable.Range(0, 20).Select(i => new string((char)('a' + i % 26), 1000)));
        var fragments = _fragmenter.Fragment(paragraphs);

        Assert.True(fragments.Count >= 2);
    }

    [Fact]
    public void CustomTokenBudget_Respected()
    {
        // Small budget = 100 tokens = 400 chars
        var md = "# First\n\n" + new string('a', 300) + "\n\n# Second\n\n" + new string('b', 300);
        var fragments = _fragmenter.Fragment(md, tokenBudget: 100);

        Assert.True(fragments.Count >= 2);
    }

    [Fact]
    public void FragmentIndices_AreZeroBased()
    {
        var section1 = "# A\n\n" + new string('a', 10000) + "\n\n";
        var section2 = "# B\n\n" + new string('b', 10000) + "\n\n";
        var section3 = "# C\n\n" + new string('c', 10000) + "\n\n";
        var md = section1 + section2 + section3;

        var fragments = _fragmenter.Fragment(md);

        Assert.Equal(0, fragments[0].Index);
        Assert.True(fragments.Count >= 2);
        Assert.Equal(fragments.Count - 1, fragments[^1].Index);
    }

    [Fact]
    public void EmptyContent_NotFragmented()
    {
        var fragments = _fragmenter.Fragment("");
        Assert.Empty(fragments);
    }

    [Fact]
    public void SingleLargeSection_SplitsAtParagraphs()
    {
        // One heading but content far exceeds budget
        var bigContent = "# Huge Section\n\n" + string.Join("\n\n", Enumerable.Range(0, 30).Select(i => new string('x', 1000)));
        var fragments = _fragmenter.Fragment(bigContent);

        Assert.True(fragments.Count >= 2);
    }
}
