using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using umbConsistec.Services;

namespace umbConsistec.Controllers;

[Route("api/downloads/email")]
public sealed class ProtectedDownloadsController(
    ProtectedDownloadService downloads,
    ILogger<ProtectedDownloadsController> logger) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("protected-downloads")]
    [RequestSizeLimit(32 * 1024)]
    [RequestFormLimits(ValueLengthLimit = 16 * 1024)]
    public async Task<IActionResult> Send(
        [FromForm] string? email,
        [FromForm] string? ticket,
        [FromForm(Name = "frc-captcha-response")] string? captchaResponse,
        CancellationToken cancellationToken)
    {
        email = email?.Trim();
        if (email is null || email.Length > 254
            || !MailAddress.TryCreate(email, out var recipient) || recipient.Address != email
            || string.IsNullOrWhiteSpace(ticket) || ticket.Length > 2048)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(captchaResponse) || captchaResponse.Length > 16 * 1024)
        {
            return UnprocessableEntity();
        }

        if (!downloads.IsConfigured || downloads.ResolveTicket(ticket) is not { } file)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            if (!await downloads.VerifyCaptchaAsync(captchaResponse, cancellationToken))
            {
                return UnprocessableEntity();
            }
            await downloads.SendAsync(file, recipient.Address);
            return NoContent();
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            // Avoid logging email addresses, captcha tokens, or attachment paths.
            logger.LogWarning("Protected download delivery failed ({ErrorType}).", error.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
