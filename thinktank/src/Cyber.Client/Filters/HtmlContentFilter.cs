using System.Text;
using AngleSharp;
using AngleSharp.Dom;

namespace Cyber.Client.Filters;

public sealed class HtmlContentFilter : IContentFilter
{
    public async Task<FilterResult> FilterAsync(Stream input, string fileName, CancellationToken ct = default)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(input, true), ct);

        // Remove script and style elements
        foreach (var element in document.QuerySelectorAll("script, style, noscript"))
            element.Remove();

        var sb = new StringBuilder();
        ConvertNode(document.Body ?? (INode)document.DocumentElement, sb);

        var text = sb.ToString().Trim();
        // Collapse excessive blank lines
        text = CollapseBlankLines(text);

        return new FilterResult
        {
            Text = text,
            ContentType = "text/markdown"
        };
    }

    private static void ConvertNode(INode node, StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText textNode:
                    var text = textNode.Data;
                    // Collapse whitespace within inline text
                    text = CollapseWhitespace(text);
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
            case "h1":
                sb.AppendLine();
                sb.Append("# ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h2":
                sb.AppendLine();
                sb.Append("## ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h3":
                sb.AppendLine();
                sb.Append("### ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h4":
                sb.AppendLine();
                sb.Append("#### ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h5":
                sb.AppendLine();
                sb.Append("##### ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

            case "h6":
                sb.AppendLine();
                sb.Append("###### ");
                ConvertNode(element, sb);
                sb.AppendLine();
                sb.AppendLine();
                break;

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
                {
                    // Handled by <pre>
                    ConvertNode(element, sb);
                }
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

            case "div" or "section" or "article" or "main" or "header" or "footer" or "nav":
                ConvertNode(element, sb);
                break;

            case "table":
                ConvertTable(element, sb);
                break;

            case "img":
                var alt = element.GetAttribute("alt") ?? "";
                var src = element.GetAttribute("src") ?? "";
                if (!string.IsNullOrEmpty(alt) || !string.IsNullOrEmpty(src))
                {
                    sb.Append($"![{alt}]({src})");
                }
                break;

            default:
                ConvertNode(element, sb);
                break;
        }
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
                {
                    sb.Append(" --- |");
                }
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
