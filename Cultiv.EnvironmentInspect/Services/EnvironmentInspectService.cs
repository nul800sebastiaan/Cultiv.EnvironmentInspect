using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Cache;

namespace Cultiv.EnvironmentInspect.Services;

public class EnvironmentInspectService : IEnvironmentInspectService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly ILogger<EnvironmentInspectService> _logger;
    private const string CacheKey = "Cultiv.EnvironmentInspect.ConfigData";
    private IDisposable? _changeTokenRegistration;

    public EnvironmentInspectService(IConfiguration configuration, AppCaches appCaches, ILogger<EnvironmentInspectService> logger)
    {
        _configuration = configuration;
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;

        // Register for configuration change notifications
        if (_configuration is IConfigurationRoot configRoot)
        {
            _changeTokenRegistration = ChangeToken.OnChange(
                () => configRoot.GetReloadToken(),
                () =>
                {
                    _logger.LogInformation("Configuration change detected, clearing environment inspect cache");
                    // Clear cache when configuration changes
                    _runtimeCache.Clear(CacheKey);
                    
                    // Re-warm the cache in the background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogDebug("Re-warming environment inspect cache after configuration change");
                            await GetEnvironmentDataAsync();
                            _logger.LogInformation("Environment inspect cache re-warmed successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to re-warm environment inspect cache after configuration change");
                        }
                    });
                });
        }
    }

    public async Task<List<EnvironmentVariable>> GetEnvironmentDataAsync()
    {
        // Use Umbraco's RuntimeCache with Get - cache indefinitely
        return await Task.Run(() => (List<EnvironmentVariable>)_runtimeCache.Get(
            CacheKey,
            () => BuildEnvironmentData())!);
    }

    private List<EnvironmentVariable> BuildEnvironmentData()
    {
        var environmentVariables = new List<EnvironmentVariable>(capacity: 200); // Pre-allocate with estimated capacity

        if (_configuration is not IConfigurationRoot configurationRoot) return environmentVariables;

        // Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Abstractions/src/ConfigurationRootExtensions.cs#L36
        void RecurseChildren(IEnumerable<IConfigurationSection> children)
        {
            foreach (var child in children)
            {
                var valueAndProvider = GetValueAndProvider(configurationRoot, child.Path);

                if (valueAndProvider.Provider != null)
                {
                    environmentVariables.Add(new EnvironmentVariable
                    {
                        Key = child.Path,
                        Value = valueAndProvider.Value,
                        Provider = valueAndProvider.Provider?.ToString(),
                    });
                }
                else
                {
                    environmentVariables.Add(new EnvironmentVariable
                    {
                        Key = child.Path
                    });
                }

                RecurseChildren(child.GetChildren());
            }
        }

        RecurseChildren(configurationRoot.GetChildren().Where(x => !string.IsNullOrEmpty(x.Path)));

        return environmentVariables;
    }

    private static (string? Value, IConfigurationProvider? Provider) GetValueAndProvider(
        IConfigurationRoot root,
        string key)
    {
        foreach (var provider in root.Providers.Reverse())
        {
            if (provider.TryGet(key, out var value))
            {
                return (value, provider);
            }
        }

        return (null, null);
    }

    public void Dispose()
    {
        _changeTokenRegistration?.Dispose();
    }
}
