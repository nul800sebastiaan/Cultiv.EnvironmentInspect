using System.Text.Json;
using System.Text.RegularExpressions;
using Cultiv.EnvironmentInspect.Configuration;
using Cultiv.EnvironmentInspect.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Hosting;

namespace Cultiv.EnvironmentInspect.Services;

public class EnvironmentInspectService : IEnvironmentInspectService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly ILogger<EnvironmentInspectService> _logger;
    private readonly IOptionsMonitor<EnvironmentInspectOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostingEnvironment _hostingEnvironment;
    private const string CacheKey = "Cultiv.EnvironmentInspect.ConfigData";
    private IDisposable? _optionsChangeRegistration;
    private DateTime _lastConfigChange = DateTime.MinValue;
    private readonly object _configChangeLock = new object();

    public EnvironmentInspectService(
        IConfiguration configuration,
        AppCaches appCaches,
        ILogger<EnvironmentInspectService> logger,
        IOptionsMonitor<EnvironmentInspectOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IHostingEnvironment hostingEnvironment)
    {
        _configuration = configuration;
        _runtimeCache = appCaches.RuntimeCache;
        _logger = logger;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _hostingEnvironment = hostingEnvironment;

        // Register for options change notifications
        _optionsChangeRegistration = _options.OnChange(opts =>
        {
            // Debounce: Ignore duplicate change notifications within 500ms
            lock (_configChangeLock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastConfigChange).TotalMilliseconds < 500)
                {
                    _logger.LogDebug("Ignoring duplicate configuration change notification (debounced)");
                    return;
                }
                _lastConfigChange = now;
            }

            _logger.LogInformation("EnvironmentInspect options changed, clearing cache");
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

    public async Task<EnvironmentInspectResponse> GetEnvironmentDataAsync()
    {
        // Use Umbraco's RuntimeCache with Get - cache indefinitely
        var variables = await Task.Run(() => (List<EnvironmentVariable>)_runtimeCache.Get(
            CacheKey,
            () => BuildEnvironmentData())!);

        // Detect environment
        var hasRedactions = variables.Any(v => !string.IsNullOrEmpty(v.RedactedMode));
        var isUmbracoCloud = DetectUmbracoCloud();
        var isLocal = DetectIsLocal(isUmbracoCloud);

        return new EnvironmentInspectResponse
        {
            Variables = variables,
            AzureWebAppAdvancedCopy = _options.CurrentValue.AzureWebAppAdvancedCopy,
            HasRedactions = hasRedactions,
            IsLocal = isLocal,
            IsUmbracoCloud = isUmbracoCloud
        };
    }

    private bool DetectUmbracoCloud()
    {
        // Check if running on Umbraco Cloud (online)
        var isRunningOnCloud = _configuration.GetValue<bool>("Umbraco:Cloud:IsRunningOnCloud");
        if (isRunningOnCloud)
        {
            return true;
        }

        // Check if configured for Umbraco Cloud (locally) - has Environment ID
        var cloudEnvironmentId = _configuration.GetValue<string>("Umbraco:Cloud:Identity:EnvironmentId");
        return !string.IsNullOrEmpty(cloudEnvironmentId);
    }

    private bool DetectIsLocal(bool isUmbracoCloud)
    {
        // If Umbraco Cloud online, definitely not local
        var isRunningOnCloud = _configuration.GetValue<bool>("Umbraco:Cloud:IsRunningOnCloud");
        if (isRunningOnCloud)
        {
            return false;
        }

        // Check if in Development environment
        var environmentName = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT")
                             ?? _configuration.GetValue<string>("DOTNET_ENVIRONMENT")
                             ?? "Production";

        var isDevelopmentEnvironment = environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase);

        // Also check if the request is from localhost
        var isLocalhost = false;
        if (_httpContextAccessor.HttpContext?.Request != null)
        {
            var host = _httpContextAccessor.HttpContext.Request.Host.Host.ToLowerInvariant();
            isLocalhost = host == "localhost" || host == "127.0.0.1" || host == "::1";
        }

        // Both conditions must be true for local development
        return isDevelopmentEnvironment && isLocalhost;
    }

    private List<EnvironmentVariable> BuildEnvironmentData()
    {
        _logger.LogDebug("Building environment data...");
        var environmentVariables = new List<EnvironmentVariable>(capacity: 200); // Pre-allocate with estimated capacity

        if (_configuration is not IConfigurationRoot configurationRoot) return environmentVariables;

        // Get current options
        var options = _options.CurrentValue;
        _logger.LogDebug("Current exclusion rules count: {Count}", options.Exclude.Count);
        foreach (var rule in options.Exclude)
        {
            _logger.LogDebug("Exclusion rule - Key: {Key}, Provider: {Provider}, ProviderType: {ProviderType}, ProviderSource: {ProviderSource}",
                rule.Key, rule.Provider, rule.ProviderType, rule.ProviderSource);
        }

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

        _logger.LogDebug("Total variables before exclusions: {Count}", environmentVariables.Count);
        // Apply exclusions
        environmentVariables = ApplyExclusions(environmentVariables, options);
        _logger.LogDebug("Total variables after exclusions: {Count}", environmentVariables.Count);

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
        var excluded = new List<string>();
        var result = variables.Where(variable =>
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
                excluded.Add(variable.Key);
                _logger.LogDebug("Excluding variable: {Key}", variable.Key);
                return false;
            }

            return true;
        }).ToList();

        _logger.LogDebug("Applied exclusions: {ExcludedCount} variables excluded out of {TotalCount}", excluded.Count, variables.Count);
        return result;
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
        var keysToRedact = rule.RedactionOptions?.Keys ?? new List<string>();

        // Try to handle as JSON first
        if (IsJson(value))
        {
            try
            {
                return RedactJsonKeys(value, keysToRedact, rule, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing JSON for redaction, falling back to string patterns");
            }
        }

        // Handle common delimited formats (connection strings, key-value pairs)
        var result = value;

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

    private bool IsJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        return (value.StartsWith("{") && value.EndsWith("}")) ||
               (value.StartsWith("[") && value.EndsWith("]"));
    }

    private string RedactJsonKeys(string json, List<string> keysToRedact, RedactionRule rule, EnvironmentInspectOptions options)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Use a dictionary to build the redacted JSON
        var redacted = RedactJsonElement(root, keysToRedact, rule, options);

        // Serialize back to JSON string with the same formatting style (compact)
        return JsonSerializer.Serialize(redacted, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private object? RedactJsonElement(JsonElement element, List<string> keysToRedact, RedactionRule rule, EnvironmentInspectOptions options)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    if (keysToRedact.Any(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Redact this property value
                        var originalValue = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString() ?? string.Empty
                            : property.Value.GetRawText();
                        obj[property.Name] = RedactNestedValue(originalValue, rule, options);
                    }
                    else
                    {
                        // Recursively process nested objects/arrays
                        obj[property.Name] = RedactJsonElement(property.Value, keysToRedact, rule, options);
                    }
                }
                return obj;

            case JsonValueKind.Array:
                var arr = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(RedactJsonElement(item, keysToRedact, rule, options));
                }
                return arr;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                return element.GetRawText();
        }
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

    public async Task<bool> ApplyConfigurationAsync(string templateName)
    {
        try
        {
            // Only allow in development environment - check via hosting environment name
            var environmentName = _configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT")
                                 ?? _configuration.GetValue<string>("DOTNET_ENVIRONMENT")
                                 ?? "Production";

            if (!environmentName.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("ApplyConfiguration called in non-development environment");
                return false;
            }

            // Validate and get the template
            var templates = GetConfigurationTemplates();
            string configJson = templateName.ToLowerInvariant() switch
            {
                "default" => templates.DefaultTemplate,
                "umbracocloud" => templates.UmbracoCloudTemplate,
                _ => throw new ArgumentException($"Unknown template name: {templateName}", nameof(templateName))
            };

            // Find the appsettings.json file in the content root
            var contentRoot = _hostingEnvironment.ApplicationPhysicalPath;
            var appsettingsPath = Path.Combine(contentRoot, "appsettings.json");

            if (!File.Exists(appsettingsPath))
            {
                _logger.LogError("appsettings.json not found at {Path}", appsettingsPath);
                return false;
            }

            // Read the existing appsettings.json as text
            var existingText = await File.ReadAllTextAsync(appsettingsPath);

            // Configure JSON options to allow comments and trailing commas
            var deserializeOptions = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            // Parse to understand structure
            var existingConfig = JsonSerializer.Deserialize<JsonElement>(existingText, deserializeOptions);
            var templateConfig = JsonSerializer.Deserialize<JsonElement>(configJson);
            var templateSection = templateConfig.GetProperty("EnvironmentInspect");

            // Try to preserve comments by replacing only the EnvironmentInspect section
            string updatedText;
            try
            {
                _logger.LogDebug("Attempting comment preservation in appsettings.json");

                if (existingConfig.TryGetProperty("EnvironmentInspect", out var existingSection))
                {
                    _logger.LogDebug("Found existing EnvironmentInspect section, performing merge");

                    // Merge the sections
                    var mergedSection = MergeJsonElements(existingSection, templateSection);

                    var serializeOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var newSectionJson = JsonSerializer.Serialize(mergedSection, serializeOptions);
                    _logger.LogDebug("Merged section JSON length: {Length} characters", newSectionJson.Length);

                    // Replace the section while preserving comments
                    _logger.LogDebug("Calling ReplaceJsonSection...");
                    updatedText = ReplaceJsonSection(existingText, "EnvironmentInspect", newSectionJson);
                    _logger.LogDebug("ReplaceJsonSection completed, result length: {Length} characters", updatedText.Length);
                }
                else
                {
                    _logger.LogDebug("No existing EnvironmentInspect section, inserting new section");

                    // Insert new section
                    var serializeOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var newSectionJson = JsonSerializer.Serialize(templateSection, serializeOptions);
                    _logger.LogDebug("New section JSON length: {Length} characters", newSectionJson.Length);

                    _logger.LogDebug("Calling InsertJsonSection...");
                    updatedText = InsertJsonSection(existingText, "EnvironmentInspect", newSectionJson);
                    _logger.LogDebug("InsertJsonSection completed, result length: {Length} characters", updatedText.Length);
                }

                // Validate the result by parsing (Skip allows comments to be present, they're just ignored)
                _logger.LogDebug("Validating result JSON is well-formed...");
                var validateOptions = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                JsonSerializer.Deserialize<JsonElement>(updatedText, validateOptions);
                _logger.LogDebug("Validation successful - JSON is well-formed");

                _logger.LogInformation("Successfully preserved comments in appsettings.json");
            }
            catch (Exception ex)
            {
                // Fallback: use full serialization (loses comments but guaranteed valid)
                _logger.LogWarning(ex, "Failed to preserve comments at step: {Message}. Falling back to full rewrite", ex.Message);

                var mergedConfig = MergeJsonElements(existingConfig, templateConfig);

                var serializeOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                updatedText = JsonSerializer.Serialize(mergedConfig, serializeOptions);
            }

            // Write the result
            await File.WriteAllTextAsync(appsettingsPath, updatedText);

            _logger.LogInformation("Successfully applied configuration to appsettings.json");

            // Clear the cache to reload with new config
            _runtimeCache.Clear(CacheKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply configuration to appsettings.json");
            return false;
        }
    }

    private JsonElement MergeJsonElements(JsonElement target, JsonElement source)
    {
        if (source.ValueKind == JsonValueKind.Object && target.ValueKind == JsonValueKind.Object)
        {
            var merged = new Dictionary<string, JsonElement>();

            // Copy all properties from target
            foreach (var property in target.EnumerateObject())
            {
                merged[property.Name] = property.Value;
            }

            // Merge properties from source
            foreach (var property in source.EnumerateObject())
            {
                if (merged.TryGetValue(property.Name, out var existingValue))
                {
                    // For arrays: preserve existing if it has content, otherwise use source
                    if (existingValue.ValueKind == JsonValueKind.Array && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        // If existing array is empty, use source array (from template)
                        // If existing array has items, preserve it (user's custom config)
                        merged[property.Name] = existingValue.GetArrayLength() > 0 ? existingValue : property.Value;
                    }
                    else if (existingValue.ValueKind == JsonValueKind.Object && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Recursively merge objects
                        merged[property.Name] = MergeJsonElements(existingValue, property.Value);
                    }
                    else
                    {
                        // For primitives (boolean, string, number, null): preserve existing value
                        // This prevents overwriting user's AzureWebAppAdvancedCopy, RedactionCharacter, etc.
                        merged[property.Name] = existingValue;
                    }
                }
                else
                {
                    // Property doesn't exist in target, add from source
                    merged[property.Name] = property.Value;
                }
            }

            // Convert back to JsonElement
            var json = JsonSerializer.Serialize(merged);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        // For non-objects, source overwrites target (shouldn't happen with the logic above)
        return source;
    }

    private string ReplaceJsonSection(string jsonText, string sectionName, string newSectionJson)
    {
        _logger.LogDebug("ReplaceJsonSection called for section: {SectionName}", sectionName);

        // Find the section using a pattern that handles nested braces
        // Pattern: "SectionName"\s*:\s*{...}
        var pattern = $@"""{Regex.Escape(sectionName)}""\s*:\s*\{{";
        var match = Regex.Match(jsonText, pattern);

        if (!match.Success)
        {
            _logger.LogDebug("Pattern match failed for section: {SectionName}", sectionName);
            throw new InvalidOperationException($"Could not find section '{sectionName}' in JSON");
        }

        _logger.LogDebug("Pattern match succeeded at index: {Index}", match.Index);

        // Find the matching closing brace
        int startIndex = match.Index;
        int braceStart = jsonText.IndexOf('{', startIndex);
        int braceCount = 1;
        int endIndex = braceStart + 1;

        _logger.LogDebug("Starting brace counting from index: {BraceStart}", braceStart);

        while (endIndex < jsonText.Length && braceCount > 0)
        {
            char c = jsonText[endIndex];

            // Skip strings to avoid counting braces inside string values
            if (c == '"')
            {
                endIndex++;
                while (endIndex < jsonText.Length && jsonText[endIndex] != '"')
                {
                    if (jsonText[endIndex] == '\\') endIndex++; // Skip escaped characters
                    endIndex++;
                }
            }
            else if (c == '{')
            {
                braceCount++;
            }
            else if (c == '}')
            {
                braceCount--;
            }

            endIndex++;
        }

        _logger.LogDebug("Found matching closing brace at index: {EndIndex}, braceCount: {BraceCount}", endIndex, braceCount);

        // Extract the section to replace
        var fullSection = jsonText.Substring(startIndex, endIndex - startIndex);
        _logger.LogDebug("Extracted section length: {Length} characters", fullSection.Length);

        // Get indentation from the original section
        var lineStart = jsonText.LastIndexOf('\n', startIndex) + 1;
        var indentation = jsonText.Substring(lineStart, startIndex - lineStart);
        indentation = new string(indentation.TakeWhile(char.IsWhiteSpace).ToArray());
        _logger.LogDebug("Detected indentation: '{Indentation}' ({Length} characters)", indentation, indentation.Length);

        // Indent the new JSON
        var indentedJson = IndentJson(newSectionJson, indentation);
        var replacement = $"\"{sectionName}\": {indentedJson}";
        _logger.LogDebug("Replacement text length: {Length} characters", replacement.Length);

        var result = jsonText.Replace(fullSection, replacement);
        _logger.LogDebug("Replace completed, result length: {Length} characters", result.Length);

        return result;
    }

    private string InsertJsonSection(string jsonText, string sectionName, string newSectionJson)
    {
        _logger.LogDebug("InsertJsonSection called for section: {SectionName}", sectionName);

        // Find the last property before the closing brace
        var lastBraceIndex = jsonText.LastIndexOf('}');

        if (lastBraceIndex == -1)
        {
            _logger.LogDebug("No closing brace found in JSON");
            throw new InvalidOperationException("Invalid JSON structure");
        }

        _logger.LogDebug("Found last closing brace at index: {Index}", lastBraceIndex);

        // Detect indentation (assume 2 spaces based on typical formatting)
        var indentation = "  ";

        // Check if we need a comma (if there's content before the closing brace)
        var beforeClosing = jsonText.Substring(0, lastBraceIndex).TrimEnd();
        bool needsComma = beforeClosing.Length > 0 && beforeClosing[beforeClosing.Length - 1] != '{';
        _logger.LogDebug("Needs comma: {NeedsComma}", needsComma);

        // Indent the new JSON
        var indentedJson = IndentJson(newSectionJson, indentation);
        var insertion = $"{(needsComma ? "," : "")}\n{indentation}\"{sectionName}\": {indentedJson}";
        _logger.LogDebug("Insertion text length: {Length} characters", insertion.Length);

        var result = jsonText.Insert(lastBraceIndex, insertion + "\n");
        _logger.LogDebug("Insert completed, result length: {Length} characters", result.Length);

        return result;
    }

    private string IndentJson(string json, string indentation)
    {
        var lines = json.Split('\n');
        var indented = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0)
            {
                // First line gets base indentation
                indented.Add(lines[i]);
            }
            else
            {
                // Subsequent lines get additional indentation
                indented.Add(indentation + lines[i]);
            }
        }

        return string.Join("\n", indented);
    }

    public ConfigurationTemplatesDto GetConfigurationTemplates()
    {
        // Default template for standard installations
        var defaultTemplate = @"{
  ""EnvironmentInspect"": {
    ""AzureWebAppAdvancedCopy"": true,
    ""Exclude"": [
      ""^APPSETTING_"",
      ""^AZURE_"",
      ""^EnvironmentInspect"",
      ""^\\$schema$""
    ],
    ""Redact"": [
      {
        ""Key"": ""ConnectionStrings:.*"",
        ""RedactionMode"": ""Advanced"",
        ""RedactionOptions"": {
          ""Keys"": [ ""Password"", ""PWD"" ],
          ""KeepFirst"": 2,
          ""KeepLast"": 2
        }
      },
      {
        ""Key"": ""Umbraco:Storage:AzureBlob:Media:ConnectionString"",
        ""RedactionMode"": ""Advanced"",
        ""RedactionOptions"": {
          ""Keys"": [ ""AccountKey"" ],
          ""KeepFirst"": 2,
          ""KeepLast"": 2
        }
      },
      {
        ""Key"": ""^WEBSITE_.*_KEY$"",
        ""RedactionMode"": ""Full""
      },
      {
        ""Key"": "".*Password$"",
        ""RedactionMode"": ""Full""
      },
      {
        ""Key"": "".*Secret(?!.*HeaderName).*"",
        ""RedactionMode"": ""Partial""
      }
    ]
  }
}";

        // Umbraco Cloud template with cloud-specific rules
        var umbracoCloudTemplate = @"{
  ""EnvironmentInspect"": {
    ""Exclude"": [
      ""^APPSETTING_"",
      ""^AZURE_"",
      ""^EnvironmentInspect"",
      ""^\\$schema$""
    ],
    ""Redact"": [
      {
        ""Key"": ""ConnectionStrings:umbracoDbDSN"",
        ""RedactionMode"": ""Advanced"",
        ""RedactionOptions"": {
          ""Keys"": [ ""Password"", ""PWD"" ],
          ""KeepFirst"": 2,
          ""KeepLast"": 2
        }
      },
      {
        ""Key"": "".*SharedAccessSignature.*"",
        ""RedactionMode"": ""Advanced"",
        ""RedactionOptions"": {
          ""Keys"": [ ""sig"" ],
          ""KeepFirst"": 2,
          ""KeepLast"": 2
        }
      },
      {
        ""Key"": ""^UMBRACO:CLOUD:EXTERNALLOGINPROVIDER:\\\\d+$"",
        ""RedactionMode"": ""Advanced"",
        ""RedactionOptions"": {
          ""Keys"": [ ""ClientSecret"" ],
          ""KeepFirst"": 2,
          ""KeepLast"": 2
        }
      },
      {
        ""Key"": "".*Secret(?!.*HeaderName).*"",
        ""RedactionMode"": ""Partial""
      },
      {
        ""Key"": "".*Password$"",
        ""RedactionMode"": ""Partial""
      },
      {
        ""Key"": ""^WEBSITE_.*_KEY$"",
        ""RedactionMode"": ""Full""
      },
      {
        ""Key"": ""Umbraco:Forms:FieldTypes:Recaptcha3:PrivateKey"",
        ""RedactionMode"": ""Partial""
      }
    ]
  }
}";

        return new ConfigurationTemplatesDto
        {
            DefaultTemplate = defaultTemplate,
            UmbracoCloudTemplate = umbracoCloudTemplate
        };
    }

    public void Dispose()
    {
        _optionsChangeRegistration?.Dispose();
    }
}
