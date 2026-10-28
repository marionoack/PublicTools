using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EmlExtract.Models;
using MimeKit;

namespace EmlExtract.Services;

/// <summary>
/// Builds a self-contained export HTML document consisting of a header block
/// (Subject, From, To, Cc, Date, Message-Id) followed by the original HTML/text body.
/// Inline "cid:" images are embedded as base64 data URIs so the exported file works
/// standalone without the original attachments.
/// </summary>
public interface IHtmlExportService
{
    string BuildExportHtml(EmlDocument document, bool removeFooter);

    string BuildExportText(EmlDocument document, bool removeFooter);
}

public sealed class HtmlExportService(IFooterRemovalService footerRemovalService) : IHtmlExportService
{
    public string BuildExportHtml(EmlDocument document, bool removeFooter)
    {
        var bodyContent = document.HtmlBody is not null
            ? EmbedInlineImages(document.HtmlBody, document.Message)
            : $"<pre>{WebUtility.HtmlEncode(document.TextBody ?? string.Empty)}</pre>";

        if (removeFooter)
        {
            bodyContent = document.HtmlBody is not null
                ? footerRemovalService.StripHtml(bodyContent)
                : $"<pre>{WebUtility.HtmlEncode(footerRemovalService.StripPlainText(document.TextBody ?? string.Empty))}</pre>";
        }

        // Parse the decoded .NET string directly (no byte round-trip / re-sniffing of
        // any <meta charset> embedded in the original mail HTML - see
        // HtmlDocumentHelper for details) so an already-complete HTML document from
        // the source mail doesn't end up nested inside another <html>/<body> either.
        var htmlDoc = new HtmlParser().ParseDocument(bodyContent);

        ApplyHeadMetadata(htmlDoc, document.Subject);
        InsertHeaderBlock(htmlDoc, document);

        // Guarantees <meta charset="utf-8"> is the first element of <head> so any
        // viewer (browser, legacy WebBrowser control) reliably renders Umlaute.
        return HtmlDocumentHelper.EnsureUtf8Charset(htmlDoc.DocumentElement.OuterHtml);
    }

    public string BuildExportText(EmlDocument document, bool removeFooter)
    {
        string bodyText;
        if (document.TextBody is not null)
        {
            bodyText = removeFooter
                ? footerRemovalService.StripPlainText(document.TextBody)
                : document.TextBody;
        }
        else
        {
            var html = document.HtmlBody ?? string.Empty;
            if (removeFooter)
            {
                html = footerRemovalService.StripHtml(html);
            }

            bodyText = ExtractPlainText(html);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Betreff: {document.Subject}");
        sb.AppendLine($"Von: {document.From}");
        sb.AppendLine($"An: {document.To}");
        if (!string.IsNullOrWhiteSpace(document.Cc))
        {
            sb.AppendLine($"Cc: {document.Cc}");
        }
        sb.AppendLine($"Datum: {document.Date}");
        if (!string.IsNullOrWhiteSpace(document.MessageId))
        {
            sb.AppendLine($"Message-Id: {document.MessageId}");
        }
        sb.AppendLine();
        sb.Append(bodyText);

        return sb.ToString();
    }

    private static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Direct string parsing (see HtmlDocumentHelper) to avoid re-sniffing the
        // encoding from any embedded <meta charset> tag.
        var doc = new HtmlParser().ParseDocument(html);
        return doc.Body?.TextContent?.Trim() ?? string.Empty;
    }

    private static void ApplyHeadMetadata(IDocument htmlDoc, string subject)
    {
        var head = htmlDoc.Head;
        if (head is null)
        {
            return;
        }

        head.QuerySelector("title")?.Remove();
        var title = htmlDoc.CreateElement("title");
        title.TextContent = subject;
        head.AppendChild(title);

        var style = htmlDoc.CreateElement("style");
        style.TextContent = "body{font-family:Segoe UI,Arial,sans-serif;} " +
                             ".eml-header{border-bottom:1px solid #ccc;margin-bottom:1em;padding-bottom:0.5em;} " +
                             ".eml-header div{margin:2px 0;}";
        head.AppendChild(style);
    }

    private static void InsertHeaderBlock(IDocument htmlDoc, EmlDocument document)
    {
        var body = htmlDoc.Body;
        if (body is null)
        {
            return;
        }

        var headerDiv = htmlDoc.CreateElement("div");
        headerDiv.ClassName = "eml-header";

        void AddLine(string label, string value)
        {
            var line = htmlDoc.CreateElement("div");
            var strong = htmlDoc.CreateElement("strong");
            strong.TextContent = $"{label}:";
            line.AppendChild(strong);
            line.AppendChild(htmlDoc.CreateTextNode($" {value}"));
            headerDiv.AppendChild(line);
        }

        AddLine("Betreff", document.Subject);
        AddLine("Von", document.From);
        AddLine("An", document.To);
        if (!string.IsNullOrWhiteSpace(document.Cc))
        {
            AddLine("Cc", document.Cc);
        }
        AddLine("Datum", document.Date);
        if (!string.IsNullOrWhiteSpace(document.MessageId))
        {
            AddLine("Message-Id", document.MessageId);
        }

        body.InsertBefore(headerDiv, body.FirstChild);
    }

    private static string EmbedInlineImages(string html, MimeMessage message)
    {
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            var contentId = part.ContentId;
            if (string.IsNullOrEmpty(contentId))
            {
                continue;
            }

            using var ms = new MemoryStream();
            part.Content?.DecodeTo(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());
            var dataUri = $"data:{part.ContentType.MimeType};base64,{base64}";

            html = html.Replace($"cid:{contentId}", dataUri, StringComparison.OrdinalIgnoreCase)
                       .Replace($"cid:{contentId.Trim('<', '>')}", dataUri, StringComparison.OrdinalIgnoreCase);
        }

        return html;
    }
}
