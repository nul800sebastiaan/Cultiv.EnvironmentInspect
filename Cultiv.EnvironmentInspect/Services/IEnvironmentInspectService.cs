namespace Cultiv.EnvironmentInspect.Services;

public interface IEnvironmentInspectService
{
    Task<List<EnvironmentVariable>> GetEnvironmentDataAsync();
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
