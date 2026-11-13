using System.Threading.RateLimiting;
using umbConsistec.Services;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace umbConsistec.Extensions;

public static class ProtectedDownloadExtensions
{
    public static IUmbracoBuilder AddProtectedDownloads(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<ProtectedDownloadOptions>(
            builder.Config.GetSection("ProtectedDownloads"));
        builder.Services.AddDataProtection();
        builder.Services.AddHttpClient<ProtectedDownloadService>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("protected-downloads", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));
        });
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
            options.AddFilter(new UmbracoPipelineFilter("ProtectedDownloadRateLimit",
                postRouting: app => app.UseRateLimiter())));
        return builder;
    }
}
