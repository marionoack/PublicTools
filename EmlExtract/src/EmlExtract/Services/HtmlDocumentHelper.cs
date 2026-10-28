using AngleSharp.Html.Parser;

namespace EmlExtract.Services;

/// <summary>
/// Ensures an HTML string is a well-formed document with an explicit UTF-8 charset
/// declared as the first element of &lt;head&gt;. This is required for the legacy
/// WPF <see cref="System.Windows.Controls.WebBrowser"/> (MSHTML) control, which
/// otherwise guesses the system codepage and renders Umlaute/special characters as
/// mojibake when fed raw HTML fragments via NavigateToString.
/// </summary>
public static class HtmlDocumentHelper
{
    public static string EnsureUtf8Charset(string html)
    {
        // Parse the already-decoded .NET string directly (char-based). Using
        // BrowsingContext.OpenAsync(req => req.Content(html)) would re-encode the
        // string to bytes and then re-sniff the encoding from any <meta charset>
        // tag embedded in the original mail HTML, silently corrupting Umlaute
        // whenever that embedded tag doesn't say "utf-8" (e.g. Outlook mails with
        // windows-1252/iso-8859-1 meta tags).
        var document = new HtmlParser().ParseDocument(html);

        var head = document.Head;
        if (head is null)
        {
            return html;
        }

        head.QuerySelector("meta[charset]")?.Remove();

        var meta = document.CreateElement("meta");
        meta.SetAttribute("charset", "utf-8");
        head.InsertBefore(meta, head.FirstChild);

        return document.DocumentElement.OuterHtml;
    }
}

