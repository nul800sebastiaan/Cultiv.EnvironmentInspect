using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Cache;

namespace Cultiv.EnvironmentInspect.Services;

public class EnvironmentInspectService : IEnvironmentInspectService
{
    private readonly IConfiguration _configuration;
    private readonly IAppPolicyCache _runtimeCache;
    private const string CacheKey = "Cultiv.EnvironmentInspect.ConfigData";

    public EnvironmentInspectService(IConfiguration configuration, AppCaches appCaches)
    {
        _configuration = configuration;
        _runtimeCache = appCaches.RuntimeCache;
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
}
