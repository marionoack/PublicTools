using EmlExtract.Services;
using Xunit;

namespace EmlExtract.Tests;

public class HtmlDocumentHelperTests
{
    [Fact]
    public void EnsureUtf8Charset_PreservesUmlauteEvenWithConflictingEmbeddedMetaTag()
    {
        // Simulates a real-world mail (e.g. from Outlook) whose HTML body already
        // declares a different charset internally, independent of the actual
        // (correctly UTF-8-decoded) .NET string content.
        var html = "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=iso-8859-1\"></head>" +
                   "<body><p>Grüße äöüÄÖÜß enthält</p></body></html>";

        var result = HtmlDocumentHelper.EnsureUtf8Charset(html);

        Assert.Contains("Grüße äöüÄÖÜß enthält", result);
        Assert.Contains("<meta charset=\"utf-8\">", result);
    }

    [Fact]
    public void EnsureUtf8Charset_PlacesMetaCharsetFirstInHead()
    {
        var html = "<html><head><title>Test</title></head><body><p>Inhalt</p></body></html>";

        var result = HtmlDocumentHelper.EnsureUtf8Charset(html);

        var headStart = result.IndexOf("<head>", StringComparison.Ordinal);
        var metaIndex = result.IndexOf("<meta charset", StringComparison.Ordinal);
        var titleIndex = result.IndexOf("<title", StringComparison.Ordinal);

        Assert.True(headStart >= 0 && metaIndex > headStart && metaIndex < titleIndex);
    }
}
