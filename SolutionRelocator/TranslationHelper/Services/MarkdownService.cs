using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TranslationHelper.Services;

public class MarkdownService
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().Build();

    public string StripMarkdown(string markdownText)
    {
        if (string.IsNullOrEmpty(markdownText))
            return markdownText;

        var document = Markdown.Parse(markdownText, _pipeline);
        var sb = new System.Text.StringBuilder();

        foreach (var node in document.Descendants())
        {
            if (node is LiteralInline literal)
            {
                sb.Append(literal.Content.ToString());
            }
            else if (node is LineBreakInline lineBreak)
            {
                sb.Append(lineBreak.IsHard ? '\n' : ' ');
            }
            else if (node is LeafBlock && node is not CodeInline)
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append('\n');
            }
        }

        return sb.ToString().Trim();
    }
}
