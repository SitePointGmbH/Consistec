namespace umbConsistec.Extensions;

public static class AppExtension
{
    public static WebApplication AddCspConfig(this WebApplication app)
    {
        app.UseCsp(options => options
            .DefaultSources(s => s.None())
            .BaseUris(s => s.Self())
            .FrameAncestors(s => s.None())
            .MediaSources(s => s.Self())
            .ScriptSources(s => s
                .Self()
                .UnsafeInline()
                .CustomSources(
                    "https://*.consistec.de",
                    "https://matomo2.consistec.de",
                    "https://*.videolyser.de")) // LinkedIn Insight Tag
            .StyleSources(s => s
                .Self()
                .UnsafeInline())
            .FontSources(s => s
                .Self())
            .ConnectSources(s => s
                .Self())
            .FrameSources(s => s
                .Self())
            .ObjectSources(s => s.None())
            .FormActions(s => s.Self())
            .UpgradeInsecureRequests()
            .BlockAllMixedContent()
        );

        return app;
    }
}