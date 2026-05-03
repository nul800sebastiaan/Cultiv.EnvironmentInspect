using Cultiv.EnvironmentInspect.Controllers;

namespace Cultiv.EnvironmentInspect.Services;

public interface IEnvironmentInspectService
{
    Task<EnvironmentInspectResponse> GetEnvironmentDataAsync();
    Task<bool> ApplyConfigurationAsync(string configJson);
    ConfigurationTemplatesDto GetConfigurationTemplates();
}

public class EnvironmentVariable
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public string? Provider { get; set; }
    public string? ProviderType { get; set; }
    public string? ProviderSource { get; set; }
    public string? RedactedMode { get; set; }
}

public class ConfigurationTemplatesDto
{
    public required string DefaultTemplate { get; set; }
    public required string UmbracoCloudTemplate { get; set; }
}
