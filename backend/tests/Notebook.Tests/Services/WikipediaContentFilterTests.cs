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

    [Fact]
    public void Filter_RemovesInlineStyleDefinitions()
    {
        var input = """
            <style data-mw-deduplicate="TemplateStyles:r886047488">
            .mw-parser-output .hatnote{font-style:italic}
            .mw-parser-output div.hatnote{padding-left:1.6em;margin-top:0.5em;margin-bottom:0.5em}
            </style>

            # Hydrogen

            Hydrogen is the lightest element.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("mw-parser-output", result.Content);
        Assert.DoesNotContain("font-style:italic", result.Content);
        Assert.Contains("Hydrogen is the lightest element.", result.Content);
    }

    [Fact]
    public void Filter_RemovesStyleReferenceLinks()
    {
        var input = """
            <link rel="mw-deduplicated-inline-style" href="mw-data:TemplateStyles:r886047488" />

            # Hydrogen

            This is the main content about hydrogen.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("mw-deduplicated-inline-style", result.Content);
        Assert.DoesNotContain("TemplateStyles", result.Content);
        Assert.Contains("main content about hydrogen", result.Content);
    }

    [Fact]
    public void Filter_RemovesScreenReaderOnlyContent()
    {
        var input = """
            # Element Properties

            The element has an <span class="sr-only">Isotope</span> atomic number.

            It has various <span class="sr-only">Alternative names</span> properties.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("sr-only", result.Content);
        Assert.DoesNotContain("Isotope", result.Content);
        Assert.DoesNotContain("Alternative names", result.Content);
        Assert.Contains("atomic number", result.Content);
        Assert.Contains("properties", result.Content);
    }

    [Fact]
    public void Filter_RemovesToCMarkupRemnants()
    {
        var input = """
            # Hydrogen

            <style data-mw-deduplicate="TemplateStyles:r886046785">.mw-parser-output .toclimit-2 .toclevel-1 ul{display:none}</style><div class="toclimit-3"><meta property="mw:PageProp/toc" /></div>

            ## Overview

            Hydrogen is the first element.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("toclimit", result.Content);
        Assert.DoesNotContain("mw:PageProp/toc", result.Content);
        Assert.Contains("first element", result.Content);
    }

    [Fact]
    public void Filter_RemovesCitationBracketSpans()
    {
        var input = """
            Hydrogen was discovered<span class="cite-bracket">[</span>1<span class="cite-bracket">]</span> in the 17th century.
            """;
        var result = _filter.Filter(input);

        // Citation brackets should be removed entirely
        Assert.DoesNotContain("cite-bracket", result.Content);
        Assert.DoesNotContain("[1]", result.Content);
        Assert.Contains("discovered", result.Content);
        Assert.Contains("17th century", result.Content);
    }

    [Fact]
    public void Filter_RemovesNoboldSpanButKeepsContent()
    {
        var input = """
            The standard atomic weight is 1.008<span class="nobold">(H)</span>.

            The melting point is <span class="nobold">−259.16 °C</span>.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("nobold", result.Content);
        Assert.Contains("(H)", result.Content);
        Assert.Contains("1.008", result.Content);
        Assert.Contains("−259.16 °C", result.Content);
        Assert.Contains("melting point", result.Content);
    }

    [Fact]
    public void Filter_RemovesEnhancedHatnotes()
    {
        var input = """
            # Hydrogen

            Main article: Hydrogen atom
            Further information: Hydrogen bonding
            See also: Noble gases

            Hydrogen is a chemical element.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("Main article:", result.Content);
        Assert.DoesNotContain("Further information:", result.Content);
        Assert.DoesNotContain("See also:", result.Content);
        Assert.DoesNotContain("Noble gases", result.Content);
        Assert.Contains("chemical element", result.Content);
    }

    [Fact]
    public void Filter_CombinedBoilerplateRemoval()
    {
        var input = """
            <style data-mw-deduplicate="TemplateStyles:r123">
            .mw-parser-output .hatnote{font-style:italic}
            </style>
            <link rel="mw-deduplicated-inline-style" href="mw-data:TemplateStyles:r456" />

            # Hydrogen

            Main article: Hydrogen atom

            Hydrogen<span class="cite-bracket">[</span>1<span class="cite-bracket">]</span> is element #1.

            It has atomic weight 1.008<span class="nobold">(H)</span>.

            ## References

            1. Smith, J. (2020)
            """;
        var result = _filter.Filter(input);

        // All boilerplate should be removed
        Assert.DoesNotContain("mw-deduplicate", result.Content);
        Assert.DoesNotContain("mw-deduplicated-inline-style", result.Content);
        Assert.DoesNotContain("Main article:", result.Content);
        Assert.DoesNotContain("cite-bracket", result.Content);
        Assert.DoesNotContain("[1]", result.Content);
        Assert.DoesNotContain("nobold", result.Content);
        Assert.DoesNotContain("References", result.Content);

        // Core content should remain
        Assert.Contains("Hydrogen", result.Content);
        Assert.Contains("element #1", result.Content);
        Assert.Contains("1.008", result.Content);
        Assert.Contains("(H)", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocHeadingDivs()
    {
        var input = """
            # Hydrogen

            ::: {.mw-heading .mw-heading2}
            ## Properties
            :::

            Properties are important.

            ::: {.mw-heading .mw-heading3}
            ### Atomic Number
            :::

            The atomic number is 1.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain(".mw-heading", result.Content);
        Assert.DoesNotContain(":::", result.Content);
        Assert.Contains("## Properties", result.Content);
        Assert.Contains("### Atomic Number", result.Content);
        Assert.Contains("Properties are important", result.Content);
        Assert.Contains("atomic number is 1", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocHatnoteBlocks()
    {
        var input = """
            # Hydrogen

            ::: {.hatnote .navigation-not-searchable role="note"}
            Main article: [Hydrogen atom](/wiki/Hydrogen_atom)
            :::

            Hydrogen is the first element.

            ::: {.hatnote .navigation-not-searchable role="note"}
            See also: Noble gases
            :::

            It forms compounds with other elements.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain(".hatnote", result.Content);
        Assert.DoesNotContain("Main article:", result.Content);
        Assert.DoesNotContain("See also:", result.Content);
        Assert.DoesNotContain("hydrogen_atom", result.Content);
        Assert.Contains("Hydrogen is the first element", result.Content);
        Assert.Contains("forms compounds", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocCitationBrackets()
    {
        var input = """
            Hydrogen was discovered[[]{.cite-bracket}1[]{.cite-bracket}] in the 17th century.

            It has many properties[[]{.cite-bracket}2[]{.cite-bracket}].
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("cite-bracket", result.Content);
        Assert.DoesNotContain("[[", result.Content);
        Assert.Contains("discovered", result.Content);
        Assert.Contains("17th century", result.Content);
        Assert.Contains("many properties", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocNowrapSpans()
    {
        var input = """
            The atomic weight is 1.008[ ]{.nowrap}u.

            Temperature is −259.16[ ]{.nowrap}°C.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain(".nowrap", result.Content);
        Assert.Contains("1.008u", result.Content);
        Assert.Contains("−259.16°C", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocMathElements()
    {
        var input = """
            The energy formula [[${\\displaystyle n=1}$]{.mwe-math-mathml-inline style="display: none;"}
            ![formula](url)]{.mwe-math-element} is fundamental.

            The result [[${E = mc^2}$]{.mwe-math-mathml-inline}image]{.mwe-math-element} is known.
            """;
        var result = _filter.Filter(input);

        Assert.DoesNotContain("mwe-math", result.Content);
        Assert.DoesNotContain("displaystyle", result.Content);
        Assert.DoesNotContain("cite-bracket", result.Content);
        // Content structure should remain
        Assert.Contains("energy formula", result.Content);
        Assert.Contains("is fundamental", result.Content);
    }

    [Fact]
    public void Filter_RemovesPandocEmptyDivs()
    {
        var input = """
            # Content

            Some text here.

            :::
            :::

            More text here.

            :::
            Some inner content
            :::

            Final text.
            """;
        var result = _filter.Filter(input);

        var lines = result.Content.Split('\n');
        // Should remove empty divs but keep structure
        Assert.Contains("Some text here", result.Content);
        Assert.Contains("More text here", result.Content);
        Assert.Contains("Final text", result.Content);
        // Verify divs are gone
        Assert.DoesNotContain(":::", result.Content);
    }

    [Fact]
    public void Filter_HandlesPandocMarkdownFromWikipedia()
    {
        var input = """
            # [Hydrogen]{.mw-page-title-main}

            ::: {.mw-heading .mw-heading2}
            ## Properties {#Properties}
            :::

            Hydrogen[[]{.cite-bracket}1[]{.cite-bracket}] is the lightest element with atomic number 1.

            ::: {.hatnote .navigation-not-searchable role="note"}
            Main article: [Hydrogen atom](/wiki/Hydrogen_atom)
            :::

            The ground state energy level is −13.6[ ]{.nowrap}eV.

            ## References

            1. Smith (2020)
            """;
        var result = _filter.Filter(input);

        // Boilerplate removed
        Assert.DoesNotContain(".mw-heading", result.Content);
        Assert.DoesNotContain(".hatnote", result.Content);
        Assert.DoesNotContain("cite-bracket", result.Content);
        Assert.DoesNotContain(".nowrap", result.Content);
        Assert.DoesNotContain("Main article:", result.Content);
        Assert.DoesNotContain("References", result.Content);

        // Content preserved
        Assert.Contains("Hydrogen", result.Content);
        Assert.Contains("lightest element", result.Content);
        Assert.Contains("atomic number 1", result.Content);
        Assert.Contains("ground state energy", result.Content);
        Assert.Contains("−13.6eV", result.Content);
    }
}
