using System.Net.Mail;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using umbConsistec.Services;

namespace umbConsistec.Controllers;

[Route("api/downloads/email")]
public sealed class ProtectedDownloadsController(
    ProtectedDownloadService downloads,
    ProtectedDownloadLeadStore leads,
    IWebHostEnvironment environment,
    ILogger<ProtectedDownloadsController> logger) : ControllerBase
{
    private const string DownloadRoute = "Consistec.ProtectedDownloadFile";

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("protected-downloads")]
    [RequestSizeLimit(32 * 1024)]
    [RequestFormLimits(ValueLengthLimit = 16 * 1024)]
    public async Task<IActionResult> Send(
        [FromForm] string? email,
        [FromForm] string? ticket,
        [FromForm] bool acceptTerms,
        [FromForm(Name = "frc-captcha-response")] string? captchaResponse,
        CancellationToken cancellationToken)
    {
        email = email?.Trim();
        if (!ModelState.IsValid || !acceptTerms || email is null || email.Length > 254
            || !MailAddress.TryCreate(email, out var recipient) || recipient.Address != email
            || string.IsNullOrWhiteSpace(ticket) || ticket.Length > 2048)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(captchaResponse) || captchaResponse.Length > 16 * 1024)
        {
            return UnprocessableEntity();
        }

        if (!downloads.IsConfigured || downloads.ResolveTicket(ticket) is not { } request
            || downloads.FindFile(request.FileName) is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            if (!await downloads.VerifyCaptchaAsync(captchaResponse, cancellationToken))
            {
                return UnprocessableEntity();
            }
            var requestedAt = DateTimeOffset.UtcNow;
            var grant = downloads.CreateGrant(request, requestedAt);
            var token = downloads.CreateDownloadToken(grant);
            var link = CreateDownloadUrl(token);
            if (link is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            await leads.CreateAsync(token, recipient.Address, grant, requestedAt.UtcDateTime);
            await downloads.SendLinkAsync(grant, recipient.Address, link);
            try
            {
                await leads.MarkSentAsync(token, DateTime.UtcNow);
            }
            catch (Exception error)
            {
                // Email has already been sent; don't prompt the visitor to send it again.
                logger.LogWarning("Protected download email status could not be recorded ({ErrorType}).", error.GetType().Name);
            }
            return NoContent();
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            // Avoid logging email addresses, captcha tokens, or attachment paths.
            logger.LogWarning("Protected download delivery failed ({ErrorType}).", error.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("/api/downloads/file", Name = DownloadRoute)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Download([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048
            || downloads.ResolveDownloadToken(token) is not { } grant)
        {
            return NotFound();
        }

        var startedAt = DateTime.UtcNow;
        if (grant.ExpiresAtUtc.UtcDateTime <= startedAt)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status410Gone,
                ContentType = "text/plain; charset=utf-8",
                Content = grant.German ? "Dieser Download-Link ist abgelaufen." : "This download link has expired."
            };
        }

        try
        {
            if (downloads.FindFile(grant.FileName) is not { } file)
                return NotFound();

            var stream = file.OpenRead();
            // Recorded when the file is served: completion callbacks are unreliable under IIS, where the
            // request-aborted token is often already cancelled once the response has finished.
            if (HttpMethods.IsGet(Request.Method))
                await RecordDownloadAsync(token, startedAt);

            return File(stream, "application/octet-stream", ProtectedDownloadService.GetDownloadName(file.Name));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    private async Task RecordDownloadAsync(string token, DateTime startedAtUtc)
    {
        try
        {
            await leads.MarkDownloadedAsync(token, startedAtUtc);
        }
        catch (Exception error)
        {
            logger.LogWarning("Protected download status could not be recorded ({ErrorType}).", error.GetType().Name);
        }
    }

    private Uri? CreateDownloadUrl(string token)
    {
        var path = Url.RouteUrl(DownloadRoute, new { token });
        if (path is null || !Url.IsLocalUrl(path))
            return null;

        if (environment.IsDevelopment() && Request.Host.Host == "localhost"
            && Request.Scheme is "http" or "https")
        {
            var origin = UriHelper.BuildAbsolute(Request.Scheme, Request.Host);
            return new Uri(new Uri(origin), path);
        }

        return downloads.PublicOrigin is { } publicOrigin ? new Uri(publicOrigin, path) : null;
    }
}
