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

        // 2. Remove inter-wiki language links block (lines of [...](https://xx.wikipedia.org/...))
        result = InterWikiLinkLine().Replace(result, "");

        // 3. Remove "N languages" line
        result = LanguageCountLine().Replace(result, "");

        // 4. Strip [edit] link text
        result = EditLinkPattern().Replace(result, "");

        // 5. Remove citation brackets: [1], [2], [12], [citation needed], etc.
        result = CitationBracketPattern().Replace(result, "");

        // 6-10. Remove boilerplate sections (See also, References, External links, Further reading, Notes)
        result = RemoveSection(result, "See also");
        result = RemoveSection(result, "References");
        result = RemoveSection(result, "External links");
        result = RemoveSection(result, "Further reading");
        result = RemoveSection(result, "Notes");

        // 11. Remove Category: link lines
        result = CategoryLinePattern().Replace(result, "");

        // 12. Remove navigation footer patterns (navbox remnants, "This article..." tables)
        result = NavboxPattern().Replace(result, "");

        // 13. Remove Wikipedia image links (![...](/static/images/...) or ![...](//upload.wikimedia.org/...))
        result = WikiImagePattern().Replace(result, "");

        // 14. Remove disambiguation / hatnote lines
        result = HatnoteLine().Replace(result, "");

        // 15. Collapse resulting blank lines (3+ newlines → 2)
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
    [GeneratedRegex(@"^(?:For other uses|This article is about|""|Not to be confused with).*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex HatnoteLine();

    // 3+ consecutive newlines
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlines();
}
