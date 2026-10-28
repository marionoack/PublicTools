using System.IO;
using MimeKit;
using MsgReader.Outlook;

namespace EmlExtract.Services;

/// <summary>
/// Converts an Outlook .msg file into a <see cref="MimeMessage"/> so that all
/// downstream services (HTML/MD/TXT export, footer removal, attachments) work
/// identically for .eml and .msg input.
/// </summary>
public static class MsgToMimeConverter
{
    public static MimeMessage Convert(string path)
    {
        using var msg = new Storage.Message(path);
        return Convert(msg);
    }

    private static MimeMessage Convert(Storage.Message msg)
    {
        var mime = new MimeMessage { Subject = msg.Subject ?? string.Empty };

        if (msg.Sender is { } sender && !string.IsNullOrWhiteSpace(sender.Email ?? sender.DisplayName))
        {
            mime.From.Add(CreateAddress(sender.DisplayName, sender.Email));
        }

        foreach (var recipient in msg.Recipients)
        {
            var address = CreateAddress(recipient.DisplayName, recipient.Email);
            switch (recipient.Type?.ToString())
            {
                case "Cc":
                    mime.Cc.Add(address);
                    break;
                case "Bcc":
                    mime.Bcc.Add(address);
                    break;
                default:
                    mime.To.Add(address);
                    break;
            }
        }

        var date = msg.SentOn ?? msg.ReceivedOn ?? msg.CreationTime;
        if (date.HasValue)
        {
            mime.Date = date.Value;
        }

        var messageId = msg.Headers?.MessageId;
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            mime.MessageId = messageId.Trim('<', '>');
        }

        var builder = new BodyBuilder
        {
            TextBody = msg.BodyText,
            HtmlBody = msg.BodyHtml
        };

        foreach (var item in msg.Attachments)
        {
            switch (item)
            {
                case Storage.Attachment attachment:
                    AddAttachment(builder, attachment);
                    break;

                case Storage.Message nested:
                    var nestedMime = Convert(nested);
                    var name = SafeName(nested.Subject, "attached-message") + ".eml";
                    var part = new MessagePart { Message = nestedMime };
                    part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = name };
                    part.ContentType.Name = name;
                    builder.Attachments.Add(part);
                    break;
            }
        }

        mime.Body = builder.ToMessageBody();
        return mime;
    }

    private static void AddAttachment(BodyBuilder builder, Storage.Attachment attachment)
    {
        if (attachment.Data is null || attachment.Data.Length == 0 || attachment.IsContactPhoto)
        {
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment.dat" : attachment.FileName;
        var contentType = ParseContentType(attachment.MimeType);

        if (attachment.IsInline && !string.IsNullOrWhiteSpace(attachment.ContentId))
        {
            var resource = builder.LinkedResources.Add(fileName, attachment.Data, contentType);
            resource.ContentId = attachment.ContentId.Trim('<', '>');
        }
        else
        {
            builder.Attachments.Add(fileName, attachment.Data, contentType);
        }
    }

    private static ContentType ParseContentType(string? mimeType)
    {
        return !string.IsNullOrWhiteSpace(mimeType) && ContentType.TryParse(mimeType, out var ct)
            ? ct
            : new ContentType("application", "octet-stream");
    }

    private static MailboxAddress CreateAddress(string? displayName, string? email)
    {
        var address = string.IsNullOrWhiteSpace(email) || !email.Contains('@')
            ? "unknown@invalid"
            : email.Trim();

        // Exchange-internal addresses (X500/EX) are not valid SMTP; keep the display name only.
        return new MailboxAddress(displayName ?? email ?? string.Empty, address);
    }

    private static string SafeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
