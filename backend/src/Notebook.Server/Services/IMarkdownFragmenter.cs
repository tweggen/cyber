namespace Notebook.Server.Services;

public record Fragment(string Content, int Index);

public interface IMarkdownFragmenter
{
    /// <summary>
    /// Split markdown content into fragments at heading boundaries.
    /// Returns empty list if content fits within the token budget (no fragmentation needed).
    /// Token estimate: content.Length / 4.
    /// </summary>
    List<Fragment> Fragment(string markdown, int tokenBudget = 4000);
}
