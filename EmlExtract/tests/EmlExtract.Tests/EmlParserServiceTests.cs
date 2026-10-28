using EmlExtract.Services;
using Xunit;

namespace EmlExtract.Tests;

public class EmlParserServiceTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.eml");

    [Fact]
    public void Parse_ExtractsHeadersAndAttachments()
    {
        var service = new EmlParserService();

        var document = service.Parse(FixturePath);

        Assert.Equal("Testnachricht mit Anhang", document.Subject);
        Assert.Contains("max.mustermann@example.com", document.From);
        Assert.Contains("erika.beispiel@example.com", document.To);
        Assert.Single(document.Attachments);
        Assert.Equal("notiz.txt", document.Attachments[0].FileName);
        Assert.Contains("eigentliche Nachrichtentext", document.HtmlBody);
        Assert.Contains("eigentliche Nachrichtentext", document.TextBody);
    }

    [Fact]
    public void Parse_Msg_ExtractsUmlautsAndAttachment()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.msg");

        var document = new EmlParserService().Parse(path);

        Assert.Equal("Prüfung Größe äöü", document.Subject);
        Assert.Contains("empfaenger@example.com", document.To);
        Assert.Contains("Grüße: Übung, Äpfel, Öl, ß", document.HtmlBody);
        Assert.Single(document.Attachments);
        Assert.Equal("att.txt", document.Attachments[0].FileName);
    }

    [Theory]
    [InlineData("a.eml", true)]
    [InlineData("a.MSG", true)]
    [InlineData("a.txt", false)]
    public void IsSupported_ChecksExtension(string path, bool expected)
        => Assert.Equal(expected, EmlParserService.IsSupported(path));
}
