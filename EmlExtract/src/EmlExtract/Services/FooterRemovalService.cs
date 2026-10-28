using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EmailReplyParser;

namespace EmlExtract.Services;

/// <summary>
/// Removes quoted reply chains and signature footers using the EmailReplyParser library
/// (plain text) plus a small curated selector/pattern list for common HTML email-client
/// footer markers (Outlook/Gmail/Apple Mail signature and quote containers).
/// </summary>
public interface IFooterRemovalService
{
    string StripPlainText(string text);

    string StripHtml(string html);
}

public sealed class FooterRemovalService : IFooterRemovalService
{
    // Common container markers used by mail clients to wrap quoted/forwarded content
    // or signatures. Kept as a short, editable list rather than a "smart" detector,
    // since generic legal/disclaimer footers have no reliable structural signature.
    private static readonly string[] HtmlQuoteSelectors =
    {
        "blockquote[type='cite']",
        "div.gmail_quote",
        "div#appendonsend",
        "div.OutlookMessageHeader",
        "div.moz-cite-prefix",
        "div.gmail_signature"
    };

    public string StripPlainText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var email = global::EmailReplyParser.EmailReplyParser.Read(text);
        var visible = email.GetVisibleText();
        return string.IsNullOrWhiteSpace(visible) ? text : visible;
    }

    public string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        // Parse the decoded .NET string directly - avoids AngleSharp re-encoding to
        // bytes and re-sniffing the encoding from any <meta charset> embedded in the
        // original mail HTML, which previously corrupted Umlaute.
        var document = new HtmlParser().ParseDocument(html);

        foreach (var selector in HtmlQuoteSelectors)
        {
            foreach (var node in document.QuerySelectorAll(selector).ToArray())
            {
                node.Remove();
            }
        }

        // Also drop a trailing "-- " signature block (RFC 3676 delimiter) rendered as
        // its own paragraph/div, mirroring EmailReplyParser's plain-text signature rule.
        var body = document.Body;
        if (body is not null)
        {
            RemoveTrailingSignatureNodes(body);
        }

        return document.DocumentElement.OuterHtml;
    }

    private static void RemoveTrailingSignatureNodes(IElement body)
    {
        var children = body.Children.ToArray();

        // Find the RFC 3676 "-- " signature delimiter and drop it plus everything
        // that follows (the signature block itself).
        for (var i = 0; i < children.Length; i++)
        {
            var text = children[i].TextContent?.Trim();
            if (text == "--" || (text is not null && text.StartsWith("-- ", StringComparison.Ordinal)))
            {
                for (var j = children.Length - 1; j >= i; j--)
                {
                    children[j].Remove();
                }

                break;
            }
        }
    }
}
