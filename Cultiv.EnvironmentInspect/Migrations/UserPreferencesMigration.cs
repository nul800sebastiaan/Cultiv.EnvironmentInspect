using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Cultiv.EnvironmentInspect.Data;

namespace Cultiv.EnvironmentInspect.Migrations;

internal class RunUserPreferencesMigration : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly EnvironmentInspectDbContext _dbContext;
    private readonly ILogger<RunUserPreferencesMigration> _logger;

    public RunUserPreferencesMigration(
        EnvironmentInspectDbContext dbContext,
        ILogger<RunUserPreferencesMigration> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RunUserPreferencesMigration.HandleAsync() called");
        
        var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        
        if (pendingMigrations.Any())
        {
            _logger.LogInformation("Running {Count} pending migrations", pendingMigrations.Count());
            await _dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Cultiv Environment Inspect migrations completed successfully");
        }
        else
        {
            _logger.LogDebug("No pending migrations");
        }
    }
}
