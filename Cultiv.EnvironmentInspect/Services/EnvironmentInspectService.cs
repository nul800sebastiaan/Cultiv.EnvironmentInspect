using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core.Cache;
using Cultiv.EnvironmentInspect.Configuration;

namespace Cultiv.EnvironmentInspect.Services;

public class EnvironmentInspectService : IEnvironmentInspectService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly ILogger<EnvironmentInspectService> _logger;
    private readonly IOptionsMonitor<EnvironmentInspectOptions> _options;
    private const string CacheKey = "Cultiv.EnvironmentInspect.ConfigData";
    private IDisposable? _changeTokenRegistration;
    private IDisposable? _optionsChangeRegistration;

    public EnvironmentInspectService(
        IConfiguration configuration, 
        AppCaches appCaches, 
        ILogger<EnvironmentInspectService> logger,
        IOptionsMonitor<EnvironmentInspectOptions> options)
    {
        _configuration = configuration;
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;
        _options = options;

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

        // Register for options change notifications
        _optionsChangeRegistration = _options.OnChange(opts =>
        {
            _logger.LogInformation("EnvironmentInspect options changed, clearing cache");
            _runtimeCache.Clear(CacheKey);
        });
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

        // Get current options
        var options = _options.CurrentValue;

        // Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Abstractions/src/ConfigurationRootExtensions.cs#L36
        void RecurseChildren(IEnumerable<IConfigurationSection> children)
        {
            foreach (var child in children)
            {
                var valueAndProvider = GetValueAndProvider(configurationRoot, child.Path);

                if (valueAndProvider.Provider != null)
                {
                    var providerString = valueAndProvider.Provider.ToString();
                    var (providerType, providerSource) = ParseProvider(providerString);

                    environmentVariables.Add(new EnvironmentVariable
                    {
                        Key = child.Path,
                        Value = valueAndProvider.Value,
                        Provider = providerString,
                        ProviderType = providerType,
                        ProviderSource = providerSource,
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

        // Apply exclusions
        environmentVariables = ApplyExclusions(environmentVariables, options);

        // Apply redactions
        environmentVariables = ApplyRedactions(environmentVariables, options);

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

    private static (string? ProviderType, string? ProviderSource) ParseProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return (null, null);
        }

        // Extract provider type (class name before first space or entire string if no space)
        var providerType = provider.Split(' ', 2)[0];

        // Extract provider source (content between single quotes)
        // Example: "JsonConfigurationProvider for 'appsettings.Development.json' (Optional)*"
        // Source would be: "appsettings.Development.json"
        string? providerSource = null;
        var match = Regex.Match(provider, @"'([^']+)'");
        if (match.Success)
        {
            providerSource = match.Groups[1].Value;
        }

        return (providerType, providerSource);
    }

    private List<EnvironmentVariable> ApplyExclusions(
        List<EnvironmentVariable> variables, 
        EnvironmentInspectOptions options)
    {
        return variables.Where(variable =>
        {
            // Check if variable matches any exclusion rule
            var matchesExclusion = options.Exclude.Any(rule =>
            {
                var matches = new List<bool>();

                // Check if rule matches by key pattern
                if (!string.IsNullOrEmpty(rule.Key))
                {
                    try
                    {
                        var regex = new Regex(rule.Key, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.Key));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in exclusion rule Key: {Pattern}", rule.Key);
                        return false;
                    }
                }

                // Check if rule matches by provider
                if (!string.IsNullOrEmpty(rule.Provider) && !string.IsNullOrEmpty(variable.Provider))
                {
                    try
                    {
                        var regex = new Regex(rule.Provider, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.Provider));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in exclusion rule Provider: {Pattern}", rule.Provider);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(rule.Provider))
                {
                    matches.Add(false);
                }

                // Check if rule matches by provider type
                if (!string.IsNullOrEmpty(rule.ProviderType) && !string.IsNullOrEmpty(variable.ProviderType))
                {
                    try
                    {
                        var regex = new Regex(rule.ProviderType, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.ProviderType));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in exclusion rule ProviderType: {Pattern}", rule.ProviderType);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(rule.ProviderType))
                {
                    matches.Add(false);
                }

                // Check if rule matches by provider source
                if (!string.IsNullOrEmpty(rule.ProviderSource) && !string.IsNullOrEmpty(variable.ProviderSource))
                {
                    try
                    {
                        var regex = new Regex(rule.ProviderSource, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.ProviderSource));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in exclusion rule ProviderSource: {Pattern}", rule.ProviderSource);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(rule.ProviderSource))
                {
                    matches.Add(false);
                }

                // All specified patterns must match (AND logic)
                return matches.Count > 0 && matches.All(m => m);
            });

            if (matchesExclusion)
            {
                return false;
            }

            // Check if we should exclude empty values
            if (options.ExcludeEmptyValues && string.IsNullOrWhiteSpace(variable.Value))
            {
                return false;
            }

            return true;
        }).ToList();
    }

    private List<EnvironmentVariable> ApplyRedactions(
        List<EnvironmentVariable> variables, 
        EnvironmentInspectOptions options)
    {
        foreach (var variable in variables)
        {
            // Find the first matching redaction rule
            var rule = options.Redact.FirstOrDefault(r =>
            {
                var matches = new List<bool>();

                // Check if rule matches by key pattern
                if (!string.IsNullOrEmpty(r.Key))
                {
                    try
                    {
                        var regex = new Regex(r.Key, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.Key));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in redaction rule Key: {Pattern}", r.Key);
                        return false;
                    }
                }

                // Check if rule matches by provider
                if (!string.IsNullOrEmpty(r.Provider) && !string.IsNullOrEmpty(variable.Provider))
                {
                    try
                    {
                        var regex = new Regex(r.Provider, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.Provider));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in redaction rule Provider: {Pattern}", r.Provider);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(r.Provider))
                {
                    // Provider pattern specified but variable has no provider
                    matches.Add(false);
                }

                // Check if rule matches by provider type
                if (!string.IsNullOrEmpty(r.ProviderType) && !string.IsNullOrEmpty(variable.ProviderType))
                {
                    try
                    {
                        var regex = new Regex(r.ProviderType, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.ProviderType));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in redaction rule ProviderType: {Pattern}", r.ProviderType);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(r.ProviderType))
                {
                    // ProviderType pattern specified but variable has no provider type
                    matches.Add(false);
                }

                // Check if rule matches by provider source
                if (!string.IsNullOrEmpty(r.ProviderSource) && !string.IsNullOrEmpty(variable.ProviderSource))
                {
                    try
                    {
                        var regex = new Regex(r.ProviderSource, RegexOptions.IgnoreCase);
                        matches.Add(regex.IsMatch(variable.ProviderSource));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid regex pattern in redaction rule ProviderSource: {Pattern}", r.ProviderSource);
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(r.ProviderSource))
                {
                    // ProviderSource pattern specified but variable has no provider source
                    matches.Add(false);
                }

                // All specified patterns must match (AND logic)
                return matches.Count > 0 && matches.All(m => m);
            });

            // Apply redaction if a rule was found
            if (rule != null && !string.IsNullOrEmpty(variable.Value))
            {
                var originalValue = variable.Value;
                var redactedValue = ApplyRedactionMode(originalValue, rule, options);
                
                // Only mark as redacted if the value actually changed
                if (redactedValue != originalValue)
                {
                    variable.Value = redactedValue;
                    variable.RedactedMode = rule.RedactionMode.ToString();
                }
            }
        }

        return variables;
    }

    private string ApplyRedactionMode(string value, RedactionRule rule, EnvironmentInspectOptions options)
    {
        return rule.RedactionMode switch
        {
            RedactionMode.Full => new string(options.RedactionCharacter[0], 8),
            RedactionMode.Partial => ApplyPartialRedaction(value, options),
            RedactionMode.Advanced => ApplyAdvancedRedaction(value, rule, options),
            _ => value
        };
    }

    private string ApplyPartialRedaction(string value, EnvironmentInspectOptions options)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= options.PartialVisibleChars * 2)
        {
            return new string(options.RedactionCharacter[0], value.Length);
        }

        var visibleChars = options.PartialVisibleChars;
        var start = value[..visibleChars];
        var end = value[^visibleChars..];
        var middleLength = value.Length - (visibleChars * 2);
        var middle = new string(options.RedactionCharacter[0], Math.Max(3, middleLength));

        return $"{start}{middle}{end}";
    }

    private string ApplyAdvancedRedaction(string value, RedactionRule rule, EnvironmentInspectOptions options)
    {
        var redactionOptions = rule.RedactionOptions;
        if (redactionOptions == null)
        {
            return new string(options.RedactionCharacter[0], 8);
        }

        // If Keys are specified, redact specific key-value pairs within the value (e.g., connection strings)
        if (redactionOptions.Keys != null && redactionOptions.Keys.Any())
        {
            return RedactNestedKeys(value, rule, options);
        }

        // If KeepFirst/KeepLast are specified, show only those characters
        if (redactionOptions.KeepFirst.HasValue || redactionOptions.KeepLast.HasValue)
        {
            var keepFirst = redactionOptions.KeepFirst ?? 0;
            var keepLast = redactionOptions.KeepLast ?? 0;

            if (value.Length <= keepFirst + keepLast)
            {
                return new string(options.RedactionCharacter[0], value.Length);
            }

            var start = keepFirst > 0 ? value[..keepFirst] : string.Empty;
            var end = keepLast > 0 ? value[^keepLast..] : string.Empty;
            var middleLength = value.Length - keepFirst - keepLast;
            var middle = new string(options.RedactionCharacter[0], Math.Max(3, middleLength));

            return $"{start}{middle}{end}";
        }

        return new string(options.RedactionCharacter[0], 8);
    }

    private string RedactNestedKeys(string value, RedactionRule rule, EnvironmentInspectOptions options)
    {
        // Handle common delimited formats (connection strings, key-value pairs)
        var result = value;
        var keysToRedact = rule.RedactionOptions?.Keys ?? new List<string>();

        foreach (var key in keysToRedact)
        {
            // Try to match patterns like "Key=Value;" or "Key:Value;" or "Key: Value"
            var patterns = new[]
            {
                $@"({Regex.Escape(key)}\s*=\s*)([^;]+)",
                $@"({Regex.Escape(key)}\s*:\s*)([^;]+)",
            };

            foreach (var pattern in patterns)
            {
                try
                {
                    result = Regex.Replace(
                        result,
                        pattern,
                        match => 
                        {
                            var keyPart = match.Groups[1].Value;
                            var valuePart = match.Groups[2].Value;
                            var redactedValue = RedactNestedValue(valuePart, rule, options);
                            return $"{keyPart}{redactedValue}";
                        },
                        RegexOptions.IgnoreCase
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error applying nested key redaction for key: {Key}", key);
                }
            }
        }

        return result;
    }

    private string RedactNestedValue(string value, RedactionRule rule, EnvironmentInspectOptions options)
    {
        var redactionOptions = rule.RedactionOptions;
        
        // If KeepFirst/KeepLast are specified, use partial redaction
        if (redactionOptions?.KeepFirst.HasValue == true || redactionOptions?.KeepLast.HasValue == true)
        {
            var keepFirst = redactionOptions.KeepFirst ?? 0;
            var keepLast = redactionOptions.KeepLast ?? 0;

            if (value.Length <= keepFirst + keepLast)
            {
                return new string(options.RedactionCharacter[0], Math.Min(value.Length, 8));
            }

            var start = keepFirst > 0 ? value[..keepFirst] : string.Empty;
            var end = keepLast > 0 ? value[^keepLast..] : string.Empty;
            var middleLength = value.Length - keepFirst - keepLast;
            var middle = new string(options.RedactionCharacter[0], Math.Max(3, Math.Min(middleLength, 8)));

            return $"{start}{middle}{end}";
        }

        // Default: full redaction with fixed length
        return new string(options.RedactionCharacter[0], 8);
    }

    public void Dispose()
    {
        _changeTokenRegistration?.Dispose();
        _optionsChangeRegistration?.Dispose();
    }
}
