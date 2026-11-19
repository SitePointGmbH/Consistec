using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace umbConsistec.Services;

internal static partial class ProtectedDownloadEmailTemplate
{
    private const string TemplateName = "protected-download.html";

    public static async Task<string> RenderAsync(string contentRoot, DownloadBoxTexts texts, string fileName,
        Uri downloadUrl, string validThrough, bool german)
    {
        var template = await File.ReadAllTextAsync(Path.Combine(contentRoot, "EmailTemplates", TemplateName));
        if (!template.Contains("{{downloadUrl}}", StringComparison.Ordinal))
            throw new InvalidDataException("The protected download email template is missing {{downloadUrl}}.");

        var encoder = HtmlEncoder.Default;
        var values = new Dictionary<string, string>
        {
            ["lang"] = german ? "de" : "en",
            ["heading"] = encoder.Encode(FillText(texts.MailHeading, fileName, validThrough)),
            ["intro"] = FillHtml(texts.MailIntroHtml, fileName, validThrough, downloadUrl),
            ["downloadUrl"] = encoder.Encode(downloadUrl.AbsoluteUri),
            ["buttonLabel"] = encoder.Encode(texts.MailButtonLabel),
            ["footer"] = FillHtml(texts.MailFooterHtml, fileName, validThrough, downloadUrl)
        };
        // Single pass, so values inserted from the backoffice are never scanned for placeholders again.
        return TemplatePlaceholder().Replace(template, match => values[match.Groups[1].Value]);
    }

    /// <summary>Fills placeholders in plain text such as the email subject.</summary>
    public static string FillText(string text, string fileName, string validThrough) =>
        TextPlaceholder().Replace(text, match => match.Groups[1].Value == "fileName" ? fileName : validThrough);

    private static string FillHtml(string html, string fileName, string validThrough, Uri siteUrl)
    {
        var encoder = HtmlEncoder.Default;
        html = TextPlaceholder().Replace(html, match =>
            encoder.Encode(match.Groups[1].Value == "fileName" ? fileName : validThrough));
        // Links from the rich text editor are site-relative; mail clients need absolute URLs.
        // The path is already HTML-encoded by the editor, so only the origin is prepended.
        var origin = siteUrl.GetLeftPart(UriPartial.Authority);
        return RelativeHref().Replace(html, match => $"{match.Groups[1].Value}{origin}{match.Groups[2].Value}");
    }

    [GeneratedRegex(@"\{\{(lang|heading|intro|downloadUrl|buttonLabel|footer)\}\}")]
    private static partial Regex TemplatePlaceholder();

    [GeneratedRegex(@"\{\{(fileName|validThrough)\}\}")]
    private static partial Regex TextPlaceholder();

    [GeneratedRegex("""(href=["'])(/(?!/)[^"']*)""", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeHref();
}
