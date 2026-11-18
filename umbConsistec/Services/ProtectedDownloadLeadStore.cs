using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace umbConsistec.Services;

public sealed class ProtectedDownloadLeadStore(IScopeProvider scopeProvider, IDataProtectionProvider dataProtection)
{
    private const string LeadQuery = """
        SELECT EncryptedEmail, DownloadName, RequestedAtUtc, ExpiresAtUtc, EmailSentAtUtc, DownloadedATUTC AS DownloadedAtUtc
        FROM consistecProtectedDownloadLead
        ORDER BY RequestedAtUtc DESC, TokenHash
        """;
    private readonly IDataProtector _emailProtector = dataProtection.CreateProtector("Consistec.ProtectedDownload.LeadEmail.v1");

    internal async Task CreateAsync(string token, string email, ProtectedDownloadGrant grant, DateTime requestedAtUtc)
    {
        using var scope = scopeProvider.CreateScope();
        await scope.Database.ExecuteAsync(
            """
            INSERT INTO consistecProtectedDownloadLead
                (TokenHash, EncryptedEmail, DownloadName, RequestedAtUtc, ExpiresAtUtc)
            VALUES (@0, @1, @2, @3, @4)
            """,
            [HashToken(token), _emailProtector.Protect(email), ProtectedDownloadService.GetDownloadName(grant.FileName),
                requestedAtUtc, grant.ExpiresAtUtc.UtcDateTime]);
        scope.Complete();
    }

    internal async Task MarkSentAsync(string token, DateTime sentAtUtc)
    {
        using var scope = scopeProvider.CreateScope();
        await scope.Database.ExecuteAsync(
            "UPDATE consistecProtectedDownloadLead SET EmailSentAtUtc = @1 WHERE TokenHash = @0",
            [HashToken(token), sentAtUtc]);
        scope.Complete();
    }

    internal async Task MarkDownloadedAsync(string token, DateTime startedAtUtc)
    {
        using var scope = scopeProvider.CreateScope();
        await scope.Database.ExecuteAsync(
            """
            UPDATE consistecProtectedDownloadLead SET DownloadedATUTC = @1
            WHERE TokenHash = @0 AND DownloadedATUTC IS NULL AND ExpiresAtUtc > @1
            """,
            [HashToken(token), startedAtUtc]);
        scope.Complete();
    }

    internal async Task<ProtectedDownloadLeadPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        using var scope = scopeProvider.CreateScope();
        var result = await scope.Database.PageAsync<LeadRow>(page, pageSize, new Sql(LeadQuery), cancellationToken);
        scope.Complete();
        var now = DateTime.UtcNow;
        return new ProtectedDownloadLeadPage(result.Items.Select(row => ToLead(row, now)).ToArray(), result.TotalItems);
    }

    internal async Task ForEachAsync(Func<ProtectedDownloadLead, Task> write, CancellationToken cancellationToken)
    {
        using var scope = scopeProvider.CreateScope();
        var now = DateTime.UtcNow;
        await foreach (var row in scope.Database.QueryAsync<LeadRow>(new Sql(LeadQuery), cancellationToken))
            await write(ToLead(row, now));
        scope.Complete();
    }

    private ProtectedDownloadLead ToLead(LeadRow row, DateTime now)
    {
        string? email;
        try
        {
            email = _emailProtector.Unprotect(row.EncryptedEmail);
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            email = null;
        }

        var expired = row.ExpiresAtUtc <= now;
        var status = row.DownloadedAtUtc.HasValue ? "Downloaded" : expired ? "Expired" : "Not downloaded";
        return new ProtectedDownloadLead(email, row.DownloadName, AsUtc(row.RequestedAtUtc), AsUtc(row.ExpiresAtUtc),
            row.EmailSentAtUtc is { } sent ? AsUtc(sent) : null,
            row.DownloadedAtUtc is { } downloaded ? AsUtc(downloaded) : null, status, expired);
    }

    private static DateTime AsUtc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class LeadRow
    {
        public string EncryptedEmail { get; set; } = "";
        public string DownloadName { get; set; } = "";
        public DateTime RequestedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? EmailSentAtUtc { get; set; }
        public DateTime? DownloadedAtUtc { get; set; }
    }
}

internal sealed record ProtectedDownloadLead(string? Email, string DownloadName, DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc, DateTime? EmailSentAtUtc, DateTime? DownloadedAtUtc, string Status, bool IsExpired);

internal sealed record ProtectedDownloadLeadPage(IReadOnlyList<ProtectedDownloadLead> Items, long Total);
