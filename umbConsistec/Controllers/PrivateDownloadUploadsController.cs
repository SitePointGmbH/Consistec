using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using umbConsistec.Services;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace umbConsistec.Controllers;

[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("consistec/private-downloads")]
[ApiExplorerSettings(GroupName = "Consistec")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class PrivateDownloadUploadsController(
    ProtectedDownloadService downloads,
    ILogger<PrivateDownloadUploadsController> logger) : ManagementApiControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProtectedDownloadService.MaxAttachmentBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProtectedDownloadService.MaxAttachmentBytes)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new ProblemDetails { Title = "Choose a file to upload." });

        try
        {
            var storedName = await downloads.StoreUploadAsync(file, cancellationToken);
            return storedName is null
                ? BadRequest(new ProblemDetails { Title = "Choose a supported document up to 10 MiB with a valid file name." })
                : Ok(new { value = storedName });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Private download upload failed ({ErrorType}).", error.GetType().Name);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Private download storage is unavailable.");
        }
    }
}
