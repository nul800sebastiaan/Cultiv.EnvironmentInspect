using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

using Cultiv.EnvironmentInspect.Data;
using Cultiv.EnvironmentInspect.Services;

namespace Cultiv.EnvironmentInspect.BackgroundJobs;

/// <summary>
/// Background job that runs weekly to clean up orphaned user preferences.
/// Removes preferences for:
/// - Configuration keys that no longer exist or are excluded
/// - Users that have been disabled for more than 3 months
/// </summary>
internal class UserPreferencesCleanupJob : IDistributedBackgroundJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ILogger<UserPreferencesCleanupJob> _logger;

    public UserPreferencesCleanupJob(
        IServiceProvider serviceProvider,
        ICoreScopeProvider scopeProvider,
        ILogger<UserPreferencesCleanupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _scopeProvider = scopeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Unique name for the distributed job
    /// </summary>
    public string Name => "Cultiv.EnvironmentInspect.UserPreferencesCleanup";

    /// <summary>
    /// Runs once per week. First execution occurs one period after registration.
    /// </summary>
    public TimeSpan Period => TimeSpan.FromDays(7);

    public async Task ExecuteAsync()
    {
        try
        {
            _logger.LogInformation("Starting user preferences cleanup job");

            // Create an Umbraco core scope for proper database transaction handling
            using ICoreScope scope = _scopeProvider.CreateCoreScope();
            
            // Create a service scope to resolve scoped services
            using var serviceScope = _serviceProvider.CreateScope();
            
            // Resolve scoped services within the scope
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<EnvironmentInspectDbContext>();
            var environmentService = serviceScope.ServiceProvider.GetRequiredService<IEnvironmentInspectService>();
            var userManager = serviceScope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
            var deletedCount = 0;

            // Get all valid configuration keys
            var environmentData = await environmentService.GetEnvironmentDataAsync();
            var validKeys = environmentData.Variables.Select(v => v.Key).ToHashSet();
            
            _logger.LogDebug("Found {Count} valid configuration keys", validKeys.Count);

            // Get all user preferences
            var allPreferences = await dbContext.UserPreferences.ToListAsync();
            _logger.LogDebug("Found {Count} user preferences to check", allPreferences.Count);

            // Remove preferences for keys that no longer exist or are excluded
            var orphanedKeyPreferences = allPreferences
                .Where(p => !p.SettingKey.StartsWith("_ui_") && !validKeys.Contains(p.SettingKey))
                .ToList();

            if (orphanedKeyPreferences.Any())
            {
                _logger.LogInformation("Found {Count} preferences for removed/excluded keys", orphanedKeyPreferences.Count);
                dbContext.UserPreferences.RemoveRange(orphanedKeyPreferences);
                deletedCount += orphanedKeyPreferences.Count;
            }

            // Get distinct user keys from remaining preferences
            var userKeys = await dbContext.UserPreferences
                .Where(p => !orphanedKeyPreferences.Contains(p))
                .Select(p => p.UserKey)
                .Distinct()
                .ToListAsync();

            _logger.LogDebug("Checking {Count} distinct users", userKeys.Count);

            var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
            var inactiveUserKeys = new List<string>();

            foreach (var userKey in userKeys)
            {
                var user = await userManager.FindByIdAsync(userKey);
                
                // Remove preferences if:
                // - User no longer exists
                // - User is disabled (not approved) and their last login was more than 3 months ago
                //   (or they've never logged in and were created more than 3 months ago)
                if (user == null)
                {
                    _logger.LogDebug("User {UserKey} no longer exists", userKey);
                    inactiveUserKeys.Add(userKey);
                }
                else if (!user.IsApproved)
                {
                    // If user is disabled and last login was more than 3 months ago (or never logged in)
                    var lastLogin = user.LastLoginDate;
                    if ((lastLogin.HasValue && lastLogin.Value < threeMonthsAgo) || !lastLogin.HasValue)
                    {
                        // Check if they've been disabled for a while by checking if they haven't logged in recently
                        // For users who never logged in, we keep their preferences (they might be newly disabled)
                        if (lastLogin.HasValue)
                        {
                            _logger.LogDebug("User {UserKey} is disabled with last login {Date}", userKey, lastLogin.Value);
                            inactiveUserKeys.Add(userKey);
                        }
                    }
                }
            }

            if (inactiveUserKeys.Any())
            {
                var inactiveUserPreferences = allPreferences
                    .Where(p => inactiveUserKeys.Contains(p.UserKey))
                    .ToList();

                _logger.LogInformation("Found {Count} preferences for {UserCount} inactive users", 
                    inactiveUserPreferences.Count, inactiveUserKeys.Count);
                
                dbContext.UserPreferences.RemoveRange(inactiveUserPreferences);
                deletedCount += inactiveUserPreferences.Count;
            }

            if (deletedCount > 0)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("User preferences cleanup completed: deleted {Count} orphaned preferences", deletedCount);
            }
            else
            {
                _logger.LogInformation("User preferences cleanup completed: no orphaned preferences found");
            }
            
            // Complete the scope to commit the transaction
            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user preferences cleanup job");
            // Don't throw - we don't want to stop the background job runner
        }
    }
}