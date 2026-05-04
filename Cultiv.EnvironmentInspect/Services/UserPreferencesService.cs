using Cultiv.EnvironmentInspect.Data;
using Microsoft.EntityFrameworkCore;

namespace Cultiv.EnvironmentInspect.Services;

internal class UserPreferencesService : IUserPreferencesService
{
    private readonly EnvironmentInspectDbContext _dbContext;
    private const string UI_SETTING_PREFIX = "_ui_";

    public UserPreferencesService(EnvironmentInspectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreferencesDto> GetUserPreferencesAsync(string userKey)
    {
        var allPreferences = await _dbContext.UserPreferences
            .Where(p => p.UserKey == userKey)
            .ToListAsync();

        var starredSettings = allPreferences
            .Where(p => !p.SettingKey.StartsWith(UI_SETTING_PREFIX) && p.IsStarred)
            .Select(p => p.SettingKey)
            .ToList();

        var uiSettings = new UISettingsDto();

        // Load UI settings from preferences
        var uiPrefs = allPreferences.Where(p => p.SettingKey.StartsWith(UI_SETTING_PREFIX)).ToList();
        foreach (var pref in uiPrefs)
        {
            if (bool.TryParse(pref.StringValue, out var boolValue))
            {
                switch (pref.SettingKey)
                {
                    case "_ui_excludeEmptyValues":
                        uiSettings.ExcludeEmptyValues = boolValue;
                        break;
                    case "_ui_onlyRedacted":
                        uiSettings.OnlyRedacted = boolValue;
                        break;
                    case "_ui_onlyStarred":
                        uiSettings.OnlyStarred = boolValue;
                        break;
                    case "_ui_replaceColonWithUnderscore":
                        uiSettings.ReplaceColonWithUnderscore = boolValue;
                        break;
                    case "_ui_showAzureColumn":
                        uiSettings.ShowAzureColumn = boolValue;
                        break;
                    case "_ui_dismissInfoPanel":
                        // Assign nullable bool - null means no preference saved
                        uiSettings.DismissInfoPanel = boolValue;
                        break;
                }
            }
        }

        return new UserPreferencesDto
        {
            StarredSettings = starredSettings,
            UISettings = uiSettings
        };
    }

    public async Task SetStarredSettingsAsync(string userKey, List<string> starredKeys)
    {
        // Get all existing preferences for this user
        var existingPrefs = await _dbContext.UserPreferences
            .Where(p => p.UserKey == userKey)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var pref in existingPrefs)
        {
            var shouldBeStarred = starredKeys.Contains(pref.SettingKey);
            if (pref.IsStarred != shouldBeStarred)
            {
                pref.IsStarred = shouldBeStarred;
                pref.ModifiedDate = now;
            }
        }

        // Add new starred items that don't exist yet
        var existingKeys = existingPrefs.Select(p => p.SettingKey).ToHashSet();
        var newKeys = starredKeys.Where(k => !existingKeys.Contains(k));

        foreach (var key in newKeys)
        {
            _dbContext.UserPreferences.Add(new UserPreference
            {
                UserKey = userKey,
                SettingKey = key,
                IsStarred = true,
                CreatedDate = now,
                ModifiedDate = now
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task ToggleStarAsync(string userKey, string settingKey)
    {
        // Use a transaction to prevent race conditions from concurrent toggle requests
        using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        try
        {
            var existing = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserKey == userKey && p.SettingKey == settingKey);

            var now = DateTime.UtcNow;

            if (existing != null)
            {
                existing.IsStarred = !existing.IsStarred;
                existing.ModifiedDate = now;
            }
            else
            {
                _dbContext.UserPreferences.Add(new UserPreference
                {
                    UserKey = userKey,
                    SettingKey = settingKey,
                    IsStarred = true,
                    CreatedDate = now,
                    ModifiedDate = now
                });
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task SaveUISettingsAsync(string userKey, UISettingsDto uiSettings)
    {
        var now = DateTime.UtcNow;

        var settingsMap = new Dictionary<string, bool>
        {
            { "_ui_excludeEmptyValues", uiSettings.ExcludeEmptyValues },
            { "_ui_onlyRedacted", uiSettings.OnlyRedacted },
            { "_ui_onlyStarred", uiSettings.OnlyStarred },
            { "_ui_replaceColonWithUnderscore", uiSettings.ReplaceColonWithUnderscore },
            { "_ui_showAzureColumn", uiSettings.ShowAzureColumn },
            { "_ui_dismissInfoPanel", uiSettings.DismissInfoPanel ?? false }
        };

        foreach (var (key, value) in settingsMap)
        {
            var existing = await _dbContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserKey == userKey && p.SettingKey == key);

            if (existing != null)
            {
                existing.StringValue = value.ToString();
                existing.ModifiedDate = now;
            }
            else
            {
                _dbContext.UserPreferences.Add(new UserPreference
                {
                    UserKey = userKey,
                    SettingKey = key,
                    StringValue = value.ToString(),
                    CreatedDate = now,
                    ModifiedDate = now
                });
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
