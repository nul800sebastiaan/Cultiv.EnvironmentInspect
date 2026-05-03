namespace Cultiv.EnvironmentInspect.Services;

public interface IUserPreferencesService
{
    Task<UserPreferencesDto> GetUserPreferencesAsync(string userKey);
    Task SetStarredSettingsAsync(string userKey, List<string> starredKeys);
    Task ToggleStarAsync(string userKey, string settingKey);
    Task SaveUISettingsAsync(string userKey, UISettingsDto uiSettings);
}

public class UserPreferencesDto
{
    public List<string> StarredSettings { get; set; } = new();
    public UISettingsDto UISettings { get; set; } = new();
}

public class UISettingsDto
{
    public bool ExcludeEmptyValues { get; set; } = true;
    public bool OnlyRedacted { get; set; } = false;
    public bool OnlyStarred { get; set; } = false;
    public bool ReplaceColonWithUnderscore { get; set; } = false;
    public bool ShowAzureColumn { get; set; } = false;
    public bool DismissInfoPanel { get; set; } = false;
}
