using System.IO;
using EmlExtract.Models;
using MimeKit;

namespace EmlExtract.Services;

/// <summary>
/// Loads and parses .eml and Outlook .msg files into <see cref="EmlDocument"/> instances
/// (MimeKit for .eml, MsgReader converted to MimeMessage for .msg).
/// </summary>
public interface IEmlParserService
{
    EmlDocument Parse(string path);
}

public sealed class EmlParserService : IEmlParserService
{
    public static readonly string[] SupportedExtensions = [".eml", ".msg"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public EmlDocument Parse(string path)
    {
        MimeMessage message;
        if (string.Equals(Path.GetExtension(path), ".msg", StringComparison.OrdinalIgnoreCase))
        {
            message = MsgToMimeConverter.Convert(path);
        }
        else
        {
            using var stream = File.OpenRead(path);
            message = MimeMessage.Load(stream);
        }

        var document = new EmlDocument
        {
            SourcePath = path,
            Message = message
        };

        foreach (var entity in message.Attachments)
        {
            string fileName;
            long size;

            switch (entity)
            {
                case MessagePart messagePart:
                    fileName = SanitizeFileName(messagePart.ContentDisposition?.FileName
                        ?? messagePart.ContentType?.Name
                        ?? "attached-message.eml");
                    using (var ms = new MemoryStream())
                    {
                        messagePart.Message.WriteTo(ms);
                        size = ms.Length;
                    }
                    document.Attachments.Add(new AttachmentItem(entity, fileName, size));
                    break;

                case MimePart mimePart:
                    fileName = SanitizeFileName(mimePart.FileName ?? "untitled.dat");
                    using (var ms = new MemoryStream())
                    {
                        mimePart.Content?.DecodeTo(ms);
                        size = ms.Length;
                    }
                    document.Attachments.Add(new AttachmentItem(entity, fileName, size));
                    break;
            }
        }

        return document;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "attachment.dat";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }
}
