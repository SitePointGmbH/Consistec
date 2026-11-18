using System.Globalization;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbConsistec.Services;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace umbConsistec.Controllers;

[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("consistec/download-leads")]
[ApiExplorerSettings(GroupName = "Consistec")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DownloadLeadsController(ProtectedDownloadLeadStore leads, IBackOfficeSecurityAccessor security)
    : ManagementApiControllerBase
{
    private const string CsvHeader = "Email,File,Status,Link expired,Requested (UTC),Email sent (UTC),Expires (UTC),Downloaded (UTC)";
    private bool HasSensitiveDataAccess => security.BackOfficeSecurity?.CurrentUser?.HasAccessToSensitiveData() == true;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (!HasSensitiveDataAccess)
            return StatusCode(StatusCodes.Status403Forbidden);
        if (page < 1)
            return BadRequest();

        return Ok(await leads.GetPageAsync(page, 50, cancellationToken));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!HasSensitiveDataAccess)
            return StatusCode(StatusCodes.Status403Forbidden);

        var csv = new MemoryStream();
        try
        {
            await using (var writer = new StreamWriter(csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true)
            {
                NewLine = "\r\n"
            })
            {
                await writer.WriteLineAsync(CsvHeader.AsMemory(), cancellationToken);
                await leads.ForEachAsync(lead =>
                {
                    var row = string.Join(',', QuoteCsv(lead.Email), QuoteCsv(lead.DownloadName), lead.Status,
                        lead.IsExpired ? "Yes" : "No", FormatDate(lead.RequestedAtUtc), FormatDate(lead.EmailSentAtUtc),
                        FormatDate(lead.ExpiresAtUtc), FormatDate(lead.DownloadedAtUtc));
                    return writer.WriteLineAsync(row.AsMemory(), cancellationToken);
                }, cancellationToken);
            }
        }
        catch
        {
            csv.Dispose();
            throw;
        }

        csv.Position = 0;
        return File(csv, "text/csv; charset=utf-8", "download-leads.csv");
    }

    private static string FormatDate(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? "";

    internal static string QuoteCsv(string? value)
    {
        value ??= "";
        var trimmed = value.AsSpan().TrimStart();
        if (value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n') ||
            (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@' or '\uFF1D' or '\uFF0B' or '\uFF0D' or '\uFF20'))
        {
            value = "'" + value;
        }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
