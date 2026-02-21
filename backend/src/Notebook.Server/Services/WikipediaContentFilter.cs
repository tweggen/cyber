using System.Text.RegularExpressions;

namespace Notebook.Server.Services;

public sealed partial class WikipediaContentFilter : IContentFilter
{
    public string SourceName => "wikipedia";

    public bool CanHandle(string content, string? sourceHint)
    {
        // Explicit source hint
        if (string.Equals(sourceHint, SourceName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Auto-detect: require 2+ Wikipedia-specific signals
        var signals = 0;

        if (EditLinkPattern().IsMatch(content)) signals++;
        if (CitationBracketPattern().IsMatch(content)) signals++;
        if (CategoryLinePattern().IsMatch(content)) signals++;
        if (WikipediaSectionPattern().IsMatch(content)) signals++;
        if (NavChromePattern().IsMatch(content)) signals++;

        return signals >= 2;
    }

    public FilterResult Filter(string content)
    {
        var result = content;

        // 1. Strip page-level navigation chrome (everything before the first # heading)
        result = StripNavigationChrome(result);

        // === PANDOC MARKDOWN BOILERPLATE (from Pandoc HTML-to-Markdown conversion) ===

        // 2. Remove Pandoc heading wrapper divs: ::: {.mw-heading .mw-heading[N]} ... :::
        result = PandocHeadingDivPattern().Replace(result, "");

        // 3. Remove Pandoc hatnote/navigation blocks: ::: {.hatnote ...} ... :::
        result = PandocHatnoteBlockPattern().Replace(result, "");

        // 4. Remove Pandoc-style citation brackets: [[]{.cite-bracket}N[]{.cite-bracket}]
        result = PandocCitationBracketPattern().Replace(result, "");

        // 5. Remove Pandoc nowrap spans: [ ]{.nowrap}
        result = PandocNowrapPattern().Replace(result, "");

        // 6. Remove Pandoc inline math elements (MathML with fallback rendering)
        result = PandocMathElementPattern().Replace(result, "");

        // 7. Remove empty Pandoc divs: ::: ... :::
        result = PandocEmptyDivPattern().Replace(result, "");

        // === HTML BOILERPLATE (from raw HTML or direct conversion) ===

        // 8. Remove inline CSS style definitions
        result = InlineCssStylePattern().Replace(result, "");

        // 9. Remove style reference links
        result = StyleLinkPattern().Replace(result, "");

        // 10. Remove inter-wiki language links block (lines of [...](https://xx.wikipedia.org/...))
        result = InterWikiLinkLine().Replace(result, "");

        // 11. Remove "N languages" line
        result = LanguageCountLine().Replace(result, "");

        // 12. Strip [edit] link text
        result = EditLinkPattern().Replace(result, "");

        // 13. Remove citation brackets: [1], [2], [12], [citation needed], etc.
        result = CitationBracketPattern().Replace(result, "");

        // 14. Remove sr-only (screen-reader only) content
        result = SrOnlyPattern().Replace(result, "");

        // 15. Remove hatnote disambiguation notices (improved)
        result = HatnoteLine().Replace(result, "");

        // 16. Remove TOC control markup
        result = TocRemnantsPattern().Replace(result, "");

        // 17-21. Remove boilerplate sections (See also, References, External links, Further reading, Notes)
        result = RemoveSection(result, "See also");
        result = RemoveSection(result, "References");
        result = RemoveSection(result, "External links");
        result = RemoveSection(result, "Further reading");
        result = RemoveSection(result, "Notes");

        // 22. Remove Category: link lines
        result = CategoryLinePattern().Replace(result, "");

        // 23. Remove navigation footer patterns (navbox remnants, "This article..." tables)
        result = NavboxPattern().Replace(result, "");

        // 24. Remove Wikipedia image links (![...](/static/images/...) or ![...](//upload.wikimedia.org/...))
        result = WikiImagePattern().Replace(result, "");

        // 25. Remove citation bracket styling spans
        result = CitationBracketSpanPattern().Replace(result, "");

        // 26. Remove nobold styling spans (keep content)
        result = NoboldSpanPattern().Replace(result, "$1");

        // 27. Collapse resulting blank lines (3+ newlines → 2)
        result = ExcessiveNewlines().Replace(result, "\n\n");

        return new FilterResult(result.Trim(), "wikipedia");
    }

    /// <summary>
    /// Strip everything before the first top-level markdown heading (# Title).
    /// This removes the sidebar navigation, search box, ToC, etc. that appear
    /// when a full Wikipedia page is converted from HTML to markdown.
    /// </summary>
    private static string StripNavigationChrome(string content)
    {
        var match = FirstH1Heading().Match(content);
        if (!match.Success)
            return content;

        return content[match.Index..];
    }

    /// <summary>
    /// Remove an entire markdown section by heading text, from its heading to either
    /// the next same-level-or-higher heading or end of content.
    /// </summary>
    private static string RemoveSection(string content, string sectionName)
    {
        // Match ## Section Name or # Section Name (any heading level)
        var pattern = @"^(#{1,6})\s+" + Regex.Escape(sectionName) + @"\s*$";
        var match = Regex.Match(content, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success)
            return content;

        var headingLevel = match.Groups[1].Value.Length;
        var sectionStart = match.Index;

        // Find next heading at same or higher level
        var restOfContent = content[(match.Index + match.Length)..];
        var nextHeadingPattern = @"^#{1," + headingLevel + @"}\s+\S";
        var nextMatch = Regex.Match(restOfContent, nextHeadingPattern, RegexOptions.Multiline);

        var sectionEnd = nextMatch.Success
            ? match.Index + match.Length + nextMatch.Index
            : content.Length;

        return string.Concat(content.AsSpan(0, sectionStart), content.AsSpan(sectionEnd));
    }

    // First # heading at start of a line (the article title)
    [GeneratedRegex(@"^# \S", RegexOptions.Multiline)]
    private static partial Regex FirstH1Heading();

    // Navigation chrome detection: sidebar links to /wiki/ paths
    [GeneratedRegex(@"\[Main page\]\(/wiki/Main_Page\)", RegexOptions.IgnoreCase)]
    private static partial Regex NavChromePattern();

    // Inter-wiki language links: - [Language](https://xx.wikipedia.org/...)
    [GeneratedRegex(@"^-?\s*\[.+?\]\(https?://[a-z-]+\.wikipedia\.org/wiki/.+?\)\s*$", RegexOptions.Multiline)]
    private static partial Regex InterWikiLinkLine();

    // "N languages" line that precedes inter-wiki links
    [GeneratedRegex(@"^\s*\d+\s+languages\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex LanguageCountLine();

    // \[edit\] links (with optional surrounding whitespace)
    [GeneratedRegex(@"\s*\[edit\]", RegexOptions.IgnoreCase)]
    private static partial Regex EditLinkPattern();

    // Citation brackets: [1], [23], [citation needed], [a], [note 1], etc.
    [GeneratedRegex(@"\[(?:\d+|[a-z]|citation needed|note \d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex CitationBracketPattern();

    // Category: link lines
    [GeneratedRegex(@"^.*\bCategory:.*$", RegexOptions.Multiline)]
    private static partial Regex CategoryLinePattern();

    // Wikipedia-specific section headings for detection
    [GeneratedRegex(@"^#{1,6}\s+(?:See also|References|External links|Further reading)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex WikipediaSectionPattern();

    // Navigation footer / navbox patterns
    [GeneratedRegex(@"^\|.*(?:This article|v\s*·\s*t\s*·\s*e).*$", RegexOptions.Multiline)]
    private static partial Regex NavboxPattern();

    // Wikipedia-hosted images (static assets or wikimedia uploads)
    [GeneratedRegex(@"!\[.*?\]\((?:/static/images/|//upload\.wikimedia\.org/).*?\)\s*", RegexOptions.None)]
    private static partial Regex WikiImagePattern();

    // Disambiguation / hatnote lines: "For other uses, see ...", "This article is about ..."
    // Enhanced to catch more disambiguation patterns across languages
    [GeneratedRegex(@"^(?:For other uses|This article is about|""|Not to be confused with|Main article:|Further information:|See also:).*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex HatnoteLine();

    // Inline CSS style definitions: <style data-mw-deduplicate="...">...</style>
    // Matches MediaWiki template style tags with CSS class definitions
    [GeneratedRegex(@"<style\s+(?:data-mw-deduplicate=""[^""]*""|id=""[^""]*"")[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineCssStylePattern();

    // Style reference links: <link rel="mw-deduplicated-inline-style" href="..." />
    // Removes style reference tags that point to external stylesheets
    [GeneratedRegex(@"<link\s+rel=""mw-deduplicated-inline-style""\s+href=""[^""]*""\s*/>", RegexOptions.IgnoreCase)]
    private static partial Regex StyleLinkPattern();

    // Screen-reader only content: <span class="sr-only">...</span>
    // These are hidden from visual display but shown to screen readers
    [GeneratedRegex(@"<span\s+class=""sr-only""[^>]*>[^<]*</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SrOnlyPattern();

    // Table of Contents control markup and remnants
    // Matches both style definitions and the toclimit div containers (in either order)
    [GeneratedRegex(@"(<style[^>]*data-mw-deduplicate=""TemplateStyles:r\d+""[^>]*>.*?toclimit.*?</style>)|(<div\s+class=""toclimit-\d""[^>]*>.*?</div>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TocRemnantsPattern();

    // Citation bracket styling spans: <span class="cite-bracket">[</span> or </span>
    // Extracts the actual bracket from the span wrapper
    [GeneratedRegex(@"<span\s+class=""cite-bracket""[^>]*>([\[\]])</span>", RegexOptions.IgnoreCase)]
    private static partial Regex CitationBracketSpanPattern();

    // Nobold styling spans: <span class="nobold">...</span>
    // Keep the content but remove the styling wrapper
    [GeneratedRegex(@"<span\s+class=""nobold""[^>]*>([^<]*)</span>", RegexOptions.IgnoreCase)]
    private static partial Regex NoboldSpanPattern();

    // === PANDOC MARKDOWN SPECIFIC PATTERNS ===

    // Pandoc heading wrapper divs: ::: {.mw-heading .mw-heading[N]} ... :::
    // These wrap section headings and add no content value. Keep the heading, remove the wrapper.
    [GeneratedRegex(@":::\s*\{\.mw-heading[^}]*\}\s*\n", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PandocHeadingDivPattern();

    // Pandoc hatnote/navigation blocks: ::: {.hatnote .navigation-not-searchable role="note"} ... :::
    // Contains disambiguation/navigation notices that should be removed
    [GeneratedRegex(@":::\s*\{\.hatnote[^}]*\}\s*\n.*?\n:::\s*", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PandocHatnoteBlockPattern();

    // Pandoc-style citation brackets: [[]{.cite-bracket}N[]{.cite-bracket}]
    // Removes the entire citation bracket construct
    [GeneratedRegex(@"\[\[\]\{\.cite-bracket\}[^\]]*\[\]\{\.cite-bracket\}\]", RegexOptions.IgnoreCase)]
    private static partial Regex PandocCitationBracketPattern();

    // Pandoc nowrap spans: [ ]{.nowrap} - non-breaking space artifacts
    [GeneratedRegex(@"\[\s+\]\{\.nowrap\}", RegexOptions.IgnoreCase)]
    private static partial Regex PandocNowrapPattern();

    // Pandoc inline math elements with MathML and fallback rendering
    // Matches: [[...content...]{.mwe-math-mathml-inline ...}...more content...]{.mwe-math-element ...}
    // These are MediaWiki math rendering artifacts from Pandoc conversion
    [GeneratedRegex(@"\[\[.+?\]\{\.mwe-math-element[^}]*\}", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PandocMathElementPattern();

    // Pandoc div closers: closing ::: markers that are now orphaned
    [GeneratedRegex(@":::\s*$", RegexOptions.Multiline)]
    private static partial Regex PandocEmptyDivPattern();

    // 3+ consecutive newlines
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlines();
}
