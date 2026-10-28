using EmlExtract.Services;
using Xunit;

namespace EmlExtract.Tests;

public class HtmlExportAndMarkdownTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.eml");

    [Fact]
    public void BuildExportHtml_IncludesHeaderBlockAndBody()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var html = service.BuildExportHtml(document, removeFooter: false);

        Assert.Contains("Testnachricht mit Anhang", html);
        Assert.Contains("max.mustermann@example.com", html);
        Assert.Contains("eigentliche Nachrichtentext", html);
        Assert.Contains("Beispiel GmbH", html);
    }

    [Fact]
    public void BuildExportHtml_PreservesUmlauteAndDeclaresUtf8Charset()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var html = service.BuildExportHtml(document, removeFooter: false);

        Assert.Contains("äöüÄÖÜß", html);
        Assert.Contains("Grüße", html);
        Assert.Contains("enthält", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        // meta charset must be the first element in <head> (required for legacy
        // WebBrowser/MSHTML to not guess the wrong codepage).
        var headStart = html.IndexOf("<head>", StringComparison.Ordinal);
        var metaIndex = html.IndexOf("<meta charset", StringComparison.Ordinal);
        var titleIndex = html.IndexOf("<title", StringComparison.Ordinal);
        Assert.True(headStart >= 0 && metaIndex > headStart && metaIndex < titleIndex);
    }

    [Fact]
    public void BuildExportHtml_DoesNotNestHtmlDocumentTwice()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var html = service.BuildExportHtml(document, removeFooter: false);

        Assert.Equal(1, CountOccurrences(html, "<html"));
        Assert.Equal(1, CountOccurrences(html, "<body"));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    [Fact]
    public void BuildExportHtml_WithRemoveFooter_StripsSignature()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var html = service.BuildExportHtml(document, removeFooter: true);

        Assert.Contains("eigentliche Nachrichtentext", html);
        Assert.DoesNotContain("Beispiel GmbH", html);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_ProducesMarkdownHeadings()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var htmlService = new HtmlExportService(new FooterRemovalService());
        var mdService = new MarkdownConversionService();

        var html = htmlService.BuildExportHtml(document, removeFooter: false);
        var markdown = mdService.ConvertHtmlToMarkdown(html);

        Assert.Contains("Testnachricht mit Anhang", markdown);
        Assert.Contains("eigentliche Nachrichtentext", markdown);
        Assert.Contains("äöüÄÖÜß", markdown);
    }

    [Fact]
    public void BuildExportText_IncludesHeaderAndPlainBodyWithUmlaute()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var text = service.BuildExportText(document, removeFooter: false);

        Assert.Contains("Betreff: Testnachricht mit Anhang", text);
        Assert.Contains("Von: ", text);
        Assert.Contains("eigentliche Nachrichtentext", text);
        Assert.Contains("äöüÄÖÜß", text);
        Assert.Contains("Beispiel GmbH", text);
        Assert.DoesNotContain("<p>", text);
    }

    [Fact]
    public void BuildExportText_WithRemoveFooter_StripsSignature()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new HtmlExportService(new FooterRemovalService());

        var text = service.BuildExportText(document, removeFooter: true);

        Assert.Contains("eigentliche Nachrichtentext", text);
        Assert.DoesNotContain("Beispiel GmbH", text);
    }
}

