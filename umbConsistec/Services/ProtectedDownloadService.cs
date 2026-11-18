using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace umbConsistec.Services;

public sealed class ProtectedDownloadOptions
{
    public string SiteKey { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "https://consistec.de";
}

internal sealed record ProtectedDownloadGrant(string FileName, DateTimeOffset ExpiresAtUtc, bool German);

public sealed class ProtectedDownloadService(
    IWebHostEnvironment environment,
    IDataProtectionProvider dataProtection,
    IOptions<ProtectedDownloadOptions> options,
    IOptionsMonitor<GlobalSettings> globalSettings,
    IEmailSender emailSender,
    HttpClient httpClient)
{
    internal const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const string UploadPrefix = "upload-";
    private const int UploadNameOffset = 40; // "upload-" + GUID in N format + "-"
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".xlsx", ".pptx", ".txt", ".csv", ".odt", ".ods"
    };
    private static readonly TimeZoneInfo ExpiryTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private readonly ITimeLimitedDataProtector _requestProtector =
        dataProtection.CreateProtector("Consistec.ProtectedDownload.Request.v2").ToTimeLimitedDataProtector();
    private readonly IDataProtector _linkProtector =
        dataProtection.CreateProtector("Consistec.ProtectedDownload.Link.v1");
    private readonly string _privateDirectory =
        Path.Combine(environment.ContentRootPath, "App_Data", "ProtectedDownloads");

    public string SiteKey => options.Value.SiteKey;
    internal Uri? PublicOrigin =>
        Uri.TryCreate(options.Value.PublicBaseUrl, UriKind.Absolute, out var url)
        && url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0
        && url.AbsolutePath == "/" && url.Query.Length == 0 && url.Fragment.Length == 0
            ? url : null;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SiteKey)
        && !string.IsNullOrWhiteSpace(options.Value.ApiKey)
        && !string.IsNullOrWhiteSpace(globalSettings.CurrentValue.Smtp?.From)
        && emailSender.CanSendRequiredEmail();

    public async Task<string?> StoreUploadAsync(IFormFile upload, CancellationToken cancellationToken)
    {
        var originalName = upload.FileName;
        if (!IsValidFileName(originalName) || originalName.Length > 200 - UploadNameOffset
            || upload.Length is <= 0 or > MaxAttachmentBytes
            || !AllowedExtensions.Contains(Path.GetExtension(originalName)))
        {
            return null;
        }

        var directory = GetPrivateDirectory(create: true)
            ?? throw new IOException("Protected downloads storage is unavailable.");
        var storedName = $"{UploadPrefix}{Guid.NewGuid():N}-{originalName}";
        var path = Path.Combine(directory.FullName, storedName);

        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        try
        {
            await using (stream)
            {
                await upload.CopyToAsync(stream, cancellationToken);
                if (stream.Length != upload.Length)
                    throw new IOException("Protected download upload was incomplete.");
            }
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return storedName;
    }

    public string? CreateTicket(string? fileName, DateOnly? validThrough, bool german)
    {
        if (validThrough is not { } date || date == DateOnly.MaxValue)
            return null;

        var nextMidnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var expiresAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextMidnight, ExpiryTimeZone));
        var now = DateTimeOffset.UtcNow;
        if (expiresAtUtc <= now || FindFile(fileName) is not { } file)
            return null;

        var grant = new ProtectedDownloadGrant(file.Name, expiresAtUtc, german);
        var ticketExpires = now.AddHours(1);
        return _requestProtector.Protect(JsonSerializer.Serialize(grant),
            ticketExpires < expiresAtUtc ? ticketExpires : expiresAtUtc);
    }

    internal ProtectedDownloadGrant? ResolveTicket(string ticket) =>
        ReadGrant(() => _requestProtector.Unprotect(ticket));

    internal string CreateDownloadToken(ProtectedDownloadGrant grant) =>
        _linkProtector.Protect(JsonSerializer.Serialize(grant));

    // An authentic expired link must still be distinguishable from an invalid token.
    internal ProtectedDownloadGrant? ResolveDownloadToken(string token) =>
        ReadGrant(() => _linkProtector.Unprotect(token));

    private static ProtectedDownloadGrant? ReadGrant(Func<string> unprotect)
    {
        try
        {
            return JsonSerializer.Deserialize<ProtectedDownloadGrant>(unprotect());
        }
        catch (Exception error) when (error is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    public async Task<bool> VerifyCaptchaAsync(string response, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://global.frcapi.com/api/v2/captcha/siteverify");
        request.Headers.Add("X-API-Key", options.Value.ApiKey);
        request.Content = JsonContent.Create(new { response, sitekey = SiteKey });
        using var result = await httpClient.SendAsync(request, cancellationToken);
        result.EnsureSuccessStatusCode();
        return (await result.Content.ReadFromJsonAsync<CaptchaResult>(cancellationToken))?.Success == true;
    }

    internal async Task SendLinkAsync(ProtectedDownloadGrant grant, string email, Uri downloadUrl)
    {
        var culture = CultureInfo.GetCultureInfo(grant.German ? "de-DE" : "en-GB");
        var lastValidDay = TimeZoneInfo.ConvertTime(grant.ExpiresAtUtc.AddTicks(-1), ExpiryTimeZone);
        var body = await ProtectedDownloadEmailTemplate.RenderAsync(environment.ContentRootPath, GetDownloadName(grant.FileName), downloadUrl,
            lastValidDay.ToString("D", culture), grant.German);
        var message = new EmailMessage(
            globalSettings.CurrentValue.Smtp!.From!,
            email,
            grant.German ? "Consistec - Ihr Download-Link" : "Consistec - Your download link",
            body,
            true);
        await emailSender.SendAsync(message, "ProtectedDownload", true, TimeSpan.FromSeconds(30));
    }

    internal static string GetDownloadName(string fileName)
    {
        return fileName.Length > UploadNameOffset
            && fileName.StartsWith(UploadPrefix, StringComparison.Ordinal)
            && fileName[UploadNameOffset - 1] == '-'
            && Guid.TryParseExact(fileName.AsSpan(UploadPrefix.Length, 32), "N", out _)
            ? fileName[UploadNameOffset..] : fileName;
    }

    private static bool IsValidFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && fileName.Length <= 200
        && !fileName.StartsWith('.') && !fileName.EndsWith('.') && !fileName.EndsWith(' ')
        && fileName.IndexOfAny(['/', '\\', ':']) < 0
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !fileName.Any(char.IsControl);

    private DirectoryInfo? GetPrivateDirectory(bool create = false)
    {
        var directory = new DirectoryInfo(_privateDirectory);
        var parent = directory.Parent!;
        if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) return null;
        if (create) directory.Create();
        return directory.Exists && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint) ? directory : null;
    }

    internal FileInfo? FindFile(string? fileName)
    {
        if (!IsValidFileName(fileName) || GetPrivateDirectory() is not { } directory) return null;
        var file = new FileInfo(Path.Combine(directory.FullName, fileName!));
        return file.Exists && !file.Attributes.HasFlag(FileAttributes.ReparsePoint)
            && file.Length is > 0 and <= MaxAttachmentBytes ? file : null;
    }

    private sealed record CaptchaResult([property: JsonPropertyName("success")] bool Success);
}
