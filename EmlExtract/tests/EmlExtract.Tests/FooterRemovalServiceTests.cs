using EmlExtract.Services;
using Xunit;

namespace EmlExtract.Tests;

public class FooterRemovalServiceTests
{
    private readonly FooterRemovalService _service = new();

    [Fact]
    public void StripPlainText_RemovesSignatureAfterDelimiter()
    {
        var text = "Hallo Erika,\n\nwichtiger Inhalt.\n\n--\nMax Mustermann\nBeispiel GmbH\nvertraulich";

        var result = _service.StripPlainText(text);

        Assert.Contains("wichtiger Inhalt", result);
        Assert.DoesNotContain("Beispiel GmbH", result);
    }

    [Fact]
    public void StripHtml_RemovesTrailingSignatureBlock()
    {
        var html = "<html><body><p>wichtiger Inhalt</p><div>-- </div><div>Max Mustermann<br/>Beispiel GmbH</div></body></html>";

        var result = _service.StripHtml(html);

        Assert.Contains("wichtiger Inhalt", result);
        Assert.DoesNotContain("Beispiel GmbH", result);
    }

    [Fact]
    public void StripHtml_RemovesGmailQuoteContainer()
    {
        var html = "<html><body><p>neue Antwort</p><div class=\"gmail_quote\">Am Datum schrieb X: alter Text</div></body></html>";

        var result = _service.StripHtml(html);

        Assert.Contains("neue Antwort", result);
        Assert.DoesNotContain("alter Text", result);
    }
}
