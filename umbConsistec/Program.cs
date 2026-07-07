using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using umbConsistec.Extensions;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddMiddleWares()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

// Forward headers needed to pass information from the reverse proxy to the application
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.28.0.0"), 16));
});

// This is needed to forward the headers from the reverse proxy to the application
app.UseForwardedHeaders();

app.AddCspConfig();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();