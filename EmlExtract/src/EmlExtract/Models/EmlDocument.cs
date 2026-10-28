using System.Collections.ObjectModel;
using MimeKit;

namespace EmlExtract.Models;

/// <summary>
/// Parsed representation of an .eml file with the fields needed by the UI and exporters.
/// </summary>
public sealed class EmlDocument
{
    public required string SourcePath { get; init; }

    public required MimeMessage Message { get; init; }

    public string Subject => Message.Subject ?? string.Empty;

    public string From => Message.From?.ToString() ?? string.Empty;

    public string To => Message.To?.ToString() ?? string.Empty;

    public string Cc => Message.Cc?.ToString() ?? string.Empty;

    public string Date => Message.Date == default ? string.Empty : Message.Date.ToString("yyyy-MM-dd HH:mm:ss zzz");

    public string MessageId => Message.MessageId ?? string.Empty;

    public string? HtmlBody => Message.HtmlBody;

    public string? TextBody => Message.TextBody;

    public ObservableCollection<AttachmentItem> Attachments { get; } = new();
}
