namespace EmlExtract.Services;

/// <summary>
/// Converts the exported HTML (header block + body) into Markdown using ReverseMarkdown.
/// </summary>
public interface IMarkdownConversionService
{
    string ConvertHtmlToMarkdown(string html);
}

public sealed class MarkdownConversionService : IMarkdownConversionService
{
    private readonly ReverseMarkdown.Converter _converter;

    public MarkdownConversionService()
    {
        var config = new ReverseMarkdown.Config
        {
            GithubFlavored = true
        };
        config.Tags.Unknown = ReverseMarkdown.Config.UnknownTagsOption.PassThrough;
        config.Formatting.RemoveComments = true;
        config.Links.SmartHref = true;

        _converter = new ReverseMarkdown.Converter(config);
    }

    public string ConvertHtmlToMarkdown(string html) => _converter.Convert(html);
}
