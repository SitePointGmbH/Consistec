using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace umbConsistec.Services;

/// <summary>
/// Download box and email texts, maintained in the "Download Box" tab of the Common Components node.
/// Every value falls back to a built-in default, so an empty field never breaks the box or the email.
/// </summary>
public sealed record DownloadBoxTexts(
    string ButtonLabel,
    string UnavailableText,
    string ModalTitle,
    string EmailLabel,
    string ConsentHtml,
    string SubmitLabel,
    string CloseLabel,
    string SendingText,
    string SuccessText,
    string InvalidText,
    string VerificationText,
    string RateLimitText,
    string MailSubject,
    string MailHeading,
    string MailIntroHtml,
    string MailButtonLabel,
    string MailFooterHtml)
{
    public static DownloadBoxTexts Defaults(bool german) => german
        ? new(
            "Download starten",
            "Dieser Download ist momentan nicht verfügbar.",
            "Download per E-Mail",
            "E-Mail-Adresse",
            "Ich akzeptiere die <a href=\"/datenschutz/\" target=\"_blank\" rel=\"noopener\">Nutzungsbedingungen</a>",
            "Per E-Mail senden",
            "Schließen",
            "Wird gesendet...",
            "Der Download-Link wurde per E-Mail gesendet.",
            "Bitte prüfen Sie Ihre E-Mail-Adresse und bestätigen Sie die Nutzungsbedingungen.",
            "Die Sicherheitsprüfung ist fehlgeschlagen. Bitte erneut versuchen.",
            "Zu viele Anfragen. Bitte versuchen Sie es später erneut.",
            "Consistec - Ihr Download-Link",
            "Ihr Download",
            "<p>{{fileName}}</p>",
            "Datei herunterladen",
            "<p>Der Link ist bis {{validThrough}} (Europe/Berlin) gültig.</p>")
        : new(
            "Download",
            "This download is currently unavailable.",
            "Download by email",
            "Email address",
            "I agree to the <a href=\"/datenschutz/\" target=\"_blank\" rel=\"noopener\">Terms &amp; Conditions</a>",
            "Send by email",
            "Close",
            "Sending...",
            "The download link was sent by email.",
            "Check your email address and accept the terms and conditions.",
            "Verification failed. Please try again.",
            "Too many requests. Please try again later.",
            "Consistec - Your download link",
            "Your download",
            "<p>{{fileName}}</p>",
            "Download file",
            "<p>This link expires on {{validThrough}} (Europe/Berlin).</p>");
}

public sealed partial class DownloadBoxTextService(
    IDocumentNavigationQueryService navigation,
    IPublishedContentCache contentCache)
{
    public const string GermanCulture = "de-DE";
    public const string EnglishCulture = "en-US";

    public DownloadBoxTexts Get(bool german)
    {
        var defaults = DownloadBoxTexts.Defaults(german);
        var node = FindCommonComponents();
        if (node is null)
            return defaults;

        var culture = german ? GermanCulture : EnglishCulture;
        string Text(string alias, string fallback) =>
            node.Value<string>(alias, culture) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
        string Html(string alias, string fallback) =>
            node.Value<IHtmlEncodedString>(alias, culture)?.ToHtmlString() is { } value && HasVisibleText(value)
                ? value.Trim() : fallback;

        return new(
            Text("downloadButtonLabel", defaults.ButtonLabel),
            Text("downloadUnavailableText", defaults.UnavailableText),
            Text("downloadModalTitle", defaults.ModalTitle),
            Text("downloadEmailLabel", defaults.EmailLabel),
            Html("downloadConsentText", defaults.ConsentHtml),
            Text("downloadSubmitLabel", defaults.SubmitLabel),
            Text("downloadCloseLabel", defaults.CloseLabel),
            Text("downloadSendingText", defaults.SendingText),
            Text("downloadSuccessText", defaults.SuccessText),
            Text("downloadInvalidText", defaults.InvalidText),
            Text("downloadVerificationText", defaults.VerificationText),
            Text("downloadRateLimitText", defaults.RateLimitText),
            Text("downloadMailSubject", defaults.MailSubject),
            Text("downloadMailHeading", defaults.MailHeading),
            Html("downloadMailIntro", defaults.MailIntroHtml),
            Text("downloadMailButtonLabel", defaults.MailButtonLabel),
            Html("downloadMailFooter", defaults.MailFooterHtml));
    }

    /// <summary>Removes a single wrapping paragraph so inline RTE text fits inside a label.</summary>
    public static string Unwrap(string html)
    {
        var match = SingleParagraph().Match(html.Trim());
        return match.Success ? match.Groups[1].Value : html;
    }

    private IPublishedContent? FindCommonComponents()
    {
        if (!navigation.TryGetRootKeysOfType("home", out var homeKeys))
            return null;

        foreach (var homeKey in homeKeys)
        {
            if (navigation.TryGetChildrenKeysOfType(homeKey, "commonComponents", out var keys)
                && keys.FirstOrDefault() is var key && key != Guid.Empty)
            {
                return contentCache.GetById(key);
            }
        }
        return null;
    }

    private static bool HasVisibleText(string html) =>
        !string.IsNullOrWhiteSpace(Tags().Replace(html, "").Replace("&nbsp;", " "));

    [GeneratedRegex(@"^<p>((?:(?!</?p[\s>]).)*)</p>$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SingleParagraph();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
