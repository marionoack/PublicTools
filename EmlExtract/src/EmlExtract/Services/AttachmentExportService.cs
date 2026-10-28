using System.IO;
using EmlExtract.Models;
using MimeKit;

namespace EmlExtract.Services;

/// <summary>
/// Saves selected attachments to disk, resolving filename collisions.
/// </summary>
public interface IAttachmentExportService
{
    IReadOnlyList<string> SaveAttachments(IEnumerable<AttachmentItem> attachments, string outputFolder);
}

public sealed class AttachmentExportService : IAttachmentExportService
{
    public IReadOnlyList<string> SaveAttachments(IEnumerable<AttachmentItem> attachments, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        var savedPaths = new List<string>();

        foreach (var attachment in attachments)
        {
            var path = ResolveUniquePath(outputFolder, attachment.FileName);

            switch (attachment.Entity)
            {
                case MessagePart messagePart:
                    using (var stream = File.Create(path))
                    {
                        messagePart.Message.WriteTo(stream);
                    }
                    break;

                case MimePart mimePart:
                    using (var stream = File.Create(path))
                    {
                        mimePart.Content?.DecodeTo(stream);
                    }
                    break;
            }

            savedPaths.Add(path);
        }

        return savedPaths;
    }

    private static string ResolveUniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(folder, $"{name} ({counter}){ext}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
