using System.Security.Cryptography;
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
}

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
    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Consistec.ProtectedDownload.v1").ToTimeLimitedDataProtector();
    private readonly string _privateDirectory =
        Path.Combine(environment.ContentRootPath, "App_Data", "ProtectedDownloads");

    public string SiteKey => options.Value.SiteKey;
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

    public string? CreateTicket(string? fileName)
    {
        var file = FindFile(fileName);
        return file is null ? null : _protector.Protect(file.Name, TimeSpan.FromHours(1));
    }

    public FileInfo? ResolveTicket(string ticket)
    {
        try
        {
            return FindFile(_protector.Unprotect(ticket));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
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

    public async Task SendAsync(FileInfo file, string email)
    {
        await using var stream = file.OpenRead();
        if (stream.Length is <= 0 or > MaxAttachmentBytes)
        {
            throw new IOException("Protected download attachment size is invalid.");
        }

        var message = new EmailMessage(
            globalSettings.CurrentValue.Smtp!.From!,
            [email], [], [], [],
            "Consistec - Download",
            "Ihr angefordertes Dokument ist beigefuegt.\nYour requested document is attached.",
            false,
            [new EmailMessageAttachment(stream, GetAttachmentName(file.Name))]);
        await emailSender.SendAsync(message, "ProtectedDownload", true, TimeSpan.FromSeconds(30));
    }

    private static string GetAttachmentName(string fileName)
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

    private FileInfo? FindFile(string? fileName)
    {
        if (!IsValidFileName(fileName) || GetPrivateDirectory() is not { } directory) return null;
        var file = new FileInfo(Path.Combine(directory.FullName, fileName!));
        return file.Exists && !file.Attributes.HasFlag(FileAttributes.ReparsePoint)
            && file.Length is > 0 and <= MaxAttachmentBytes ? file : null;
    }

    private sealed record CaptchaResult([property: JsonPropertyName("success")] bool Success);
}
