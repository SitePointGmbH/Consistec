using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace umbConsistec.Migrations;

public sealed class CreateProtectedDownloadLeads(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        Create.Table("consistecProtectedDownloadLead")
            .WithColumn("TokenHash").AsString(64).PrimaryKey("PK_consistecProtectedDownloadLead")
            .WithColumn("EncryptedEmail").AsString(2048).NotNullable()
            .WithColumn("DownloadName").AsString(200).NotNullable()
            .WithColumn("RequestedAtUtc").AsDateTime().NotNullable()
            .WithColumn("ExpiresAtUtc").AsDateTime().NotNullable()
            .WithColumn("EmailSentAtUtc").AsDateTime().Nullable()
            .WithColumn("DownloadedATUTC").AsDateTime().Nullable()
            .Do();

        return Task.CompletedTask;
    }
}

public sealed class ProtectedDownloadLeadMigrationHandler(
    IMigrationPlanExecutor executor,
    ICoreScopeProvider scopeProvider,
    IKeyValueService keyValueService,
    ILogger<ProtectedDownloadLeadMigrationHandler> logger) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
            return;

        var plan = new MigrationPlan("Consistec.ProtectedDownloadLeads")
            .From(string.Empty)
            .To<CreateProtectedDownloadLeads>("download-leads-v1");
        var result = await new Upgrader(plan).ExecuteAsync(executor, scopeProvider, keyValueService);
        if (!result.Successful)
            logger.LogError("Protected download lead migration did not complete; new requests will be unavailable.");
    }
}
