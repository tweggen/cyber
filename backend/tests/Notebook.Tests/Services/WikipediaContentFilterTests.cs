using Notebook.Server.Services;

namespace Notebook.Tests.Services;

public class WikipediaContentFilterTests
{
    private readonly WikipediaContentFilter _filter = new();
    private readonly ContentFilterPipeline _pipeline = new([new WikipediaContentFilter()]);

    [Fact]
    public void Filter_RemovesCitationBrackets()
    {
        var input = "Einstein[1] was a physicist[2] who developed[12] relativity.";
        var result = _filter.Filter(input);

        Assert.Equal("Einstein was a physicist who developed relativity.", result.Content);
        Assert.Equal("wikipedia", result.DetectedSource);
    }

    [Fact]
    public void Filter_RemovesCitationNeeded()
    {
        var input = "This claim is unverified[citation needed] but interesting.";
        var result = _filter.Filter(input);

        Assert.Equal("This claim is unverified but interesting.", result.Content);
    }

    [Fact]
    public void Filter_RemovesEditLinks()
    {
        var input = "## History [edit]\n\nThe history of physics is long.";
        var result = _filter.Filter(input);

        Assert.Equal("## History\n\nThe history of physics is long.", result.Content);
    }

    [Fact]
    public void Filter_RemovesReferencesSection()
    {
        var input = "Some content about physics.\n\n## References\n\n1. Smith, J. (2020).\n2. Doe, A. (2021).";
        var result = _filter.Filter(input);

        Assert.Equal("Some content about physics.", result.Content);
    }

    [Fact]
    public void Filter_RemovesSeeAlsoSection()
    {
        var input = "Main article content.\n\n## See also\n\n- Related topic 1\n- Related topic 2";
        var result = _filter.Filter(input);

        Assert.Equal("Main article content.", result.Content);
    }

    [Fact]
    public void Filter_RemovesExternalLinksSection()
    {
        var input = "Article text here.\n\n## External links\n\n- [Official site](https://example.com)";
        var result = _filter.Filter(input);

        Assert.Equal("Article text here.", result.Content);
    }

    [Fact]
    public void Filter_RemovesFurtherReadingSection()
    {
        var input = "Some content.\n\n## Further reading\n\n- Book 1\n- Book 2";
        var result = _filter.Filter(input);

        Assert.Equal("Some content.", result.Content);
    }

    [Fact]
    public void Filter_PreservesSectionAfterRemoved()
    {
        var input = "Main content.\n\n## See also\n\n- Link 1\n\n## Next section\n\nMore content here.";
        var result = _filter.Filter(input);

        Assert.Contains("Main content.", result.Content);
        Assert.Contains("## Next section", result.Content);
        Assert.Contains("More content here.", result.Content);
        Assert.DoesNotContain("See also", result.Content);
    }

    [Fact]
    public void Filter_RemovesCategoryLinks()
    {
        var input = "Article content.\n\nCategory:Physics\nCategory:Science";
        var result = _filter.Filter(input);

        Assert.Contains("Article content.", result.Content);
        Assert.DoesNotContain("Category:", result.Content);
    }

    [Fact]
    public void Filter_PreservesContentParagraphs()
    {
        var input = "Albert Einstein was a German-born theoretical physicist.\n\nHe developed the theory of relativity, one of the two pillars of modern physics.";
        var result = _filter.Filter(input);

        Assert.Contains("Albert Einstein was a German-born theoretical physicist.", result.Content);
        Assert.Contains("He developed the theory of relativity", result.Content);
    }

    [Fact]
    public void Filter_CollapsesExcessiveBlankLines()
    {
        var input = "Paragraph one.\n\n\n\n\nParagraph two.";
        var result = _filter.Filter(input);

        Assert.Equal("Paragraph one.\n\nParagraph two.", result.Content);
    }

    [Fact]
    public void CanHandle_DetectsWikipediaContent()
    {
        var content = """
            # Albert Einstein [edit]

            Albert Einstein[1] was a physicist[2] who developed relativity.

            ## See also

            - Physics
            - Relativity

            ## References

            1. Smith (2020)

            Category:Physics
            """;

        Assert.True(_filter.CanHandle(content, null));
    }

    [Fact]
    public void CanHandle_ReturnsFalseForPlainText()
    {
        var content = "This is a plain text document about physics and science. It has no Wikipedia formatting at all.";

        Assert.False(_filter.CanHandle(content, null));
    }

    [Fact]
    public void CanHandle_ReturnsFalseForSingleSignal()
    {
        // Only one signal (References heading) — should not trigger
        var content = "Some content.\n\n## References\n\n1. A book.";

        Assert.False(_filter.CanHandle(content, null));
    }

    [Fact]
    public void Filter_StripsNavigationChrome()
    {
        var input = """
            [Jump to content](#bodyContent)        Main menu
            - [Main page](/wiki/Main_Page)
            - [Contents](/wiki/Wikipedia:Contents)
            - [Current events](/wiki/Portal:Current_events)

                 Contribute
            - [Help](/wiki/Help:Contents)
            - [Community portal](/wiki/Wikipedia:Community_portal)

            # Macrofamily

            A macrofamily is a proposed grouping of language families.
            """;
        var result = _filter.Filter(input);

        Assert.StartsWith("# Macrofamily", result.Content);
        Assert.Contains("A macrofamily is a proposed grouping", result.Content);
        Assert.DoesNotContain("Main menu", result.Content);
        Assert.DoesNotContain("Jump to content", result.Content);
    }

    [Fact]
    public void Filter_StripsInterWikiLanguageLinks()
    {
        var input = """
            # Macrofamily

            Article content here.

                19 languages
            - [Deutsch](https://de.wikipedia.org/wiki/Makrofamilie)
            - [Español](https://es.wikipedia.org/wiki/Macrofamilia)
            - [Français](https://fr.wikipedia.org/wiki/Superfamille_(linguistique))
            """;
        var result = _filter.Filter(input);

        Assert.Contains("Article content here.", result.Content);
        Assert.DoesNotContain("Deutsch", result.Content);
        Assert.DoesNotContain("wikipedia.org", result.Content);
        Assert.DoesNotContain("19 languages", result.Content);
    }

    [Fact]
    public void CanHandle_DetectsNavChrome()
    {
        var content = """
            [Jump to content](#bodyContent)
            - [Main page](/wiki/Main_Page)

            # Some Article [edit]

            Content here[1].
            """;

        Assert.True(_filter.CanHandle(content, null));
    }

    [Fact]
    public void Pipeline_UsesExplicitHint()
    {
        var content = "Einstein[1] was a physicist[2].";
        var result = _pipeline.Apply(content, "wikipedia");

        Assert.Equal("Einstein was a physicist.", result.Content);
        Assert.Equal("wikipedia", result.DetectedSource);
    }

    [Fact]
    public void Pipeline_FallsBackToAutoDetect()
    {
        var content = "Einstein[1] was a physicist[2].\n\n## References\n\n1. Source\n\nCategory:Physics";
        var result = _pipeline.Apply(content, null);

        Assert.DoesNotContain("[1]", result.Content);
        Assert.DoesNotContain("References", result.Content);
        Assert.Equal("wikipedia", result.DetectedSource);
    }

    [Fact]
    public void Pipeline_PassesThroughUnknownSource()
    {
        var content = "Just some plain text content.";
        var result = _pipeline.Apply(content, "unknown_source");

        Assert.Equal("Just some plain text content.", result.Content);
        Assert.Null(result.DetectedSource);
    }

    [Fact]
    public void Pipeline_PassesThroughNoMatch()
    {
        var content = "Normal content with no platform boilerplate.";
        var result = _pipeline.Apply(content, null);

        Assert.Equal("Normal content with no platform boilerplate.", result.Content);
        Assert.Null(result.DetectedSource);
    }
}
