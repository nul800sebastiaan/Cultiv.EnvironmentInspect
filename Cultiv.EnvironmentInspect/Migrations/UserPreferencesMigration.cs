using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

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

            try
            {
                await _dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Cultiv Environment Inspect migrations completed successfully");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("already exists"))
            {
                await HandleExistingTableAsync(pendingMigrations, cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 2714) // SQL Server: "There is already an object named"
            {
                await HandleExistingTableAsync(pendingMigrations, cancellationToken);
            }
            catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                await HandleExistingTableAsync(pendingMigrations, cancellationToken);
            }
        }
        else
        {
            _logger.LogDebug("No pending migrations");
        }
    }

    private async Task HandleExistingTableAsync(IEnumerable<string> pendingMigrations, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Migration detected existing database objects. The table exists but the migration wasn't recorded.");
        _logger.LogInformation("Attempting to sync migration history...");

        try
        {
            // Manually record the migrations as applied
            foreach (var migrationId in pendingMigrations)
            {
                var isSqlite = _dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;

                var sql = isSqlite
                    ? $"INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{migrationId}', '10.0.0')"
                    : $"IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '{migrationId}') " +
                      $"INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{migrationId}', '10.0.0')";

                await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                _logger.LogInformation("Marked migration {MigrationId} as applied", migrationId);
            }

            _logger.LogInformation("Migration history synchronized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync migration history. Manual intervention required.");
            _logger.LogWarning("To manually fix: INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('{MigrationId}', '10.0.0')", pendingMigrations.First());
        }
    }
}
