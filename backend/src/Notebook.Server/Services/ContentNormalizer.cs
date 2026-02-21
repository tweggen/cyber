using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Notebook.Server.Services;

/// <summary>
/// Converts HTML content to markdown. Passes through all other content types unchanged.
/// Uses the same conversion rules as Cyber.Client's HtmlContentFilter.
/// </summary>
public sealed class ContentNormalizer : IContentNormalizer
{
    private static readonly HtmlParser Parser = new(new HtmlParserOptions(), BrowsingContext.New(AngleSharp.Configuration.Default));

    public NormalizeResult Normalize(string content, string contentType)
    {
        if (!contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
            return new NormalizeResult(content, contentType, null);

        var document = Parser.ParseDocument(content);

        // Remove script and style elements
        foreach (var element in document.QuerySelectorAll("script, style, noscript"))
            element.Remove();

        var sb = new StringBuilder();
        ConvertNode(document.Body ?? (INode)document.DocumentElement, sb);

        var markdown = CollapseBlankLines(sb.ToString().Trim());

        return new NormalizeResult(markdown, "text/markdown", "text/html");
    }

    private static void ConvertNode(INode node, StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText textNode:
                    var text = CollapseWhitespace(textNode.Data);
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                    break;
                case IElement element:
                    ConvertElement(element, sb);
                    break;
            }
        }
    }

    private static void ConvertElement(IElement element, StringBuilder sb)
    {
        var tag = element.TagName.ToLowerInvariant();

        switch (tag)
        {
            case "h1": EmitHeading(element, sb, "# "); break;
            case "h2": EmitHeading(element, sb, "## "); break;
            case "h3": EmitHeading(element, sb, "### "); break;
            case "h4": EmitHeading(element, sb, "#### "); break;
            case "h5": EmitHeading(element, sb, "##### "); break;
            case "h6": EmitHeading(element, sb, "###### "); break;

            case "p":
                sb.AppendLine();
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "br":
                sb.AppendLine();
                break;

            case "strong" or "b":
                sb.Append("**");
                ConvertNode(element, sb);
                sb.Append("**");
                break;

            case "em" or "i":
                sb.Append('*');
                ConvertNode(element, sb);
                sb.Append('*');
                break;

            case "code":
                if (element.ParentElement?.TagName.Equals("PRE", StringComparison.OrdinalIgnoreCase) == true)
                    ConvertNode(element, sb);
                else
                {
                    sb.Append('`');
                    ConvertNode(element, sb);
                    sb.Append('`');
                }
                break;

            case "pre":
                sb.AppendLine();
                sb.AppendLine("```");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine();
                break;

            case "a":
                var href = element.GetAttribute("href");
                sb.Append('[');
                ConvertNode(element, sb);
                sb.Append(']');
                if (!string.IsNullOrEmpty(href))
                {
                    sb.Append('(');
                    sb.Append(href);
                    sb.Append(')');
                }
                break;

            case "ul":
                sb.AppendLine();
                foreach (var li in element.Children)
                {
                    if (li.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append("- ");
                        ConvertNode(li, sb);
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
                break;

            case "ol":
                sb.AppendLine();
                var index = 1;
                foreach (var li in element.Children)
                {
                    if (li.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append($"{index}. ");
                        ConvertNode(li, sb);
                        sb.AppendLine();
                        index++;
                    }
                }
                sb.AppendLine();
                break;

            case "blockquote":
                sb.AppendLine();
                var quoteContent = new StringBuilder();
                ConvertNode(element, quoteContent);
                foreach (var line in quoteContent.ToString().Split('\n'))
                {
                    sb.Append("> ");
                    sb.AppendLine(line);
                }
                sb.AppendLine();
                break;

            case "hr":
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                break;

            case "table":
                ConvertTable(element, sb);
                break;

            case "img":
                var alt = element.GetAttribute("alt") ?? "";
                var src = element.GetAttribute("src") ?? "";
                if (!string.IsNullOrEmpty(alt) || !string.IsNullOrEmpty(src))
                    sb.Append($"![{alt}]({src})");
                break;

            default:
                ConvertNode(element, sb);
                break;
        }
    }

    private static void EmitHeading(IElement element, StringBuilder sb, string prefix)
    {
        sb.AppendLine();
        sb.Append(prefix);
        ConvertNode(element, sb);
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void ConvertTable(IElement table, StringBuilder sb)
    {
        sb.AppendLine();
        var rows = table.QuerySelectorAll("tr");
        var isFirstRow = true;

        foreach (var row in rows)
        {
            var cells = row.QuerySelectorAll("th, td");
            sb.Append('|');
            foreach (var cell in cells)
            {
                sb.Append(' ');
                var cellContent = new StringBuilder();
                ConvertNode(cell, cellContent);
                sb.Append(cellContent.ToString().Trim().Replace("\n", " "));
                sb.Append(" |");
            }
            sb.AppendLine();

            if (isFirstRow)
            {
                sb.Append('|');
                foreach (var _ in cells)
                    sb.Append(" --- |");
                sb.AppendLine();
                isFirstRow = false;
            }
        }
        sb.AppendLine();
    }

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    private static string CollapseBlankLines(string text)
    {
        var lines = text.Split('\n');
        var result = new StringBuilder();
        var blankCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                blankCount++;
                if (blankCount <= 2)
                    result.AppendLine();
            }
            else
            {
                blankCount = 0;
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }
}
