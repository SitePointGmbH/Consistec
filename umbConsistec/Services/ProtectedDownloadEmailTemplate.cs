using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace umbConsistec.Services;

internal static class ProtectedDownloadEmailTemplate
{
    public static async Task<string> RenderAsync(string contentRoot, string fileName, Uri downloadUrl,
        string validThrough, bool german)
    {
        var templateName = german ? "protected-download.de.html" : "protected-download.en.html";
        var template = await File.ReadAllTextAsync(Path.Combine(contentRoot, "EmailTemplates", templateName));
        if (!template.Contains("{{downloadUrl}}", StringComparison.Ordinal))
            throw new InvalidDataException("The protected download email template is missing {{downloadUrl}}.");

        var encoder = HtmlEncoder.Default;
        return Regex.Replace(template, @"\{\{(fileName|downloadUrl|validThrough)\}\}", match => match.Groups[1].Value switch
        {
            "fileName" => encoder.Encode(fileName),
            "downloadUrl" => encoder.Encode(downloadUrl.AbsoluteUri),
            _ => encoder.Encode(validThrough)
        });
    }
}
