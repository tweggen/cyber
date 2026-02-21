using System.Text.RegularExpressions;

namespace Notebook.Server.Services;

/// <summary>
/// Splits markdown at heading boundaries, then paragraph boundaries if sections are too large.
/// Token estimate: content.Length / 4.
/// </summary>
public sealed partial class MarkdownFragmenter : IMarkdownFragmenter
{
    private const int CharsPerToken = 4;

    public List<Fragment> Fragment(string markdown, int tokenBudget = 4000)
    {
        var charBudget = tokenBudget * CharsPerToken;

        // If content fits within budget, no fragmentation needed
        if (markdown.Length <= charBudget)
            return [];

        // Split at heading boundaries (lines starting with # )
        var sections = SplitAtHeadings(markdown);

        // Merge small sections and split large ones to stay within budget
        var fragments = new List<Fragment>();
        var current = "";
        var index = 0;

        foreach (var section in sections)
        {
            if (section.Length > charBudget)
            {
                // Flush current accumulator first
                if (!string.IsNullOrWhiteSpace(current))
                {
                    fragments.Add(new Fragment(current.Trim(), index++));
                    current = "";
                }

                // Split oversized section at paragraph boundaries
                foreach (var chunk in SplitAtParagraphs(section, charBudget))
                {
                    fragments.Add(new Fragment(chunk.Trim(), index++));
                }
            }
            else if ((current.Length + section.Length) > charBudget)
            {
                // Current + section would exceed budget — flush current
                if (!string.IsNullOrWhiteSpace(current))
                {
                    fragments.Add(new Fragment(current.Trim(), index++));
                }
                current = section;
            }
            else
            {
                current += section;
            }
        }

        // Flush remainder
        if (!string.IsNullOrWhiteSpace(current))
        {
            fragments.Add(new Fragment(current.Trim(), index));
        }

        // If we ended up with only 1 fragment, no point in fragmenting
        if (fragments.Count <= 1)
            return [];

        return fragments;
    }

    /// <summary>
    /// Split markdown text at heading lines (any line starting with one or more #).
    /// Each returned section includes the heading line that starts it (except possibly the first
    /// section if the document doesn't start with a heading).
    /// </summary>
    private static List<string> SplitAtHeadings(string markdown)
    {
        var sections = new List<string>();
        var lines = markdown.Split('\n');
        var current = new List<string>();

        foreach (var line in lines)
        {
            if (HeadingRegex().IsMatch(line) && current.Count > 0)
            {
                sections.Add(string.Join('\n', current) + "\n");
                current = [];
            }
            current.Add(line);
        }

        if (current.Count > 0)
            sections.Add(string.Join('\n', current) + "\n");

        return sections;
    }

    /// <summary>
    /// Split a section at double-newline (paragraph) boundaries to fit within charBudget.
    /// </summary>
    private static List<string> SplitAtParagraphs(string section, int charBudget)
    {
        var paragraphs = ParagraphSplitRegex().Split(section);
        var chunks = new List<string>();
        var current = "";

        foreach (var para in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(para))
                continue;

            if ((current.Length + para.Length) > charBudget && !string.IsNullOrWhiteSpace(current))
            {
                chunks.Add(current);
                current = para;
            }
            else
            {
                current += (string.IsNullOrEmpty(current) ? "" : "\n\n") + para;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
            chunks.Add(current);

        return chunks;
    }

    [GeneratedRegex(@"^#{1,6}\s", RegexOptions.None)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\n\s*\n", RegexOptions.None)]
    private static partial Regex ParagraphSplitRegex();
}
