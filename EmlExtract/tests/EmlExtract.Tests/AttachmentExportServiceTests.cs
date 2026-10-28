using EmlExtract.Services;
using Xunit;

namespace EmlExtract.Tests;

public class AttachmentExportServiceTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.eml");

    [Fact]
    public void SaveAttachments_WritesSelectedFilesToOutputFolder()
    {
        var document = new EmlParserService().Parse(FixturePath);
        var service = new AttachmentExportService();
        var outputFolder = Path.Combine(Path.GetTempPath(), "EmlExtractTests_" + Guid.NewGuid());

        try
        {
            var saved = service.SaveAttachments(document.Attachments, outputFolder);

            Assert.Single(saved);
            Assert.True(File.Exists(saved[0]));
            var content = File.ReadAllText(saved[0]);
            Assert.Equal("Hello Test Attachment Content", content);
        }
        finally
        {
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, recursive: true);
            }
        }
    }
}
