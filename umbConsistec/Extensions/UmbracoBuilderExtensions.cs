using Microsoft.AspNetCore.StaticFiles;
using umbConsistec.Middleware;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
namespace umbConsistec.Extensions;

public static class UmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddMiddleWares(this IUmbracoBuilder builder)
    {
        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter("NoSniffMiddleware",
                endpoints: app => app.UseMiddleware<ResponseHeaderMiddleware>()));
        });

        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });

        return builder;
    }
}