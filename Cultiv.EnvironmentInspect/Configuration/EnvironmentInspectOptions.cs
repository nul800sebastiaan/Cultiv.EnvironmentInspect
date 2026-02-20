namespace Cultiv.EnvironmentInspect.Configuration;

/// <summary>
/// Configuration options for the Environment Inspector
/// </summary>
public class EnvironmentInspectOptions
{
    /// <summary>
    /// List of exclusion rules for configuration keys/providers to exclude entirely.
    /// Can be either strings (for key patterns) or objects (for provider-based exclusion).
    /// Note: Binding is handled manually in the composer.
    /// </summary>
    public List<ExclusionRule> Exclude { get; set; } = new();

    /// <summary>
    /// List of redaction rules to apply to configuration values
    /// </summary>
    public List<RedactionRule> Redact { get; set; } = new();

    /// <summary>
    /// Character to use for redaction (default: •)
    /// </summary>
    public string RedactionCharacter { get; set; } = "•";

    /// <summary>
    /// Number of characters to show in Partial redaction mode (default: 4)
    /// </summary>
    public int PartialVisibleChars { get; set; } = 4;
}

/// <summary>
/// A rule for redacting configuration values
/// </summary>
public class RedactionRule
{
    /// <summary>
    /// Regex pattern for matching configuration keys (mutually exclusive with Provider)
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Provider name to match (mutually exclusive with Key)
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Provider type (class name) to match. Case-insensitive regex pattern.
    /// Example: "JsonConfigurationProvider" or ".*Azure.*"
    /// </summary>
    public string? ProviderType { get; set; }

    /// <summary>
    /// Provider source (file name or identifier) to match. Case-insensitive regex pattern.
    /// Example: "appsettings.Development.json" or ".*Development.*"
    /// </summary>
    public string? ProviderSource { get; set; }

    /// <summary>
    /// The redaction mode to apply
    /// </summary>
    public RedactionMode RedactionMode { get; set; }

    /// <summary>
    /// Additional options for Advanced redaction mode
    /// </summary>
    public RedactionOptions? RedactionOptions { get; set; }
}

/// <summary>
/// Advanced redaction options
/// </summary>
public class RedactionOptions
{
    /// <summary>
    /// Number of characters to keep visible at the start
    /// </summary>
    public int? KeepFirst { get; set; }

    /// <summary>
    /// Number of characters to keep visible at the end
    /// </summary>
    public int? KeepLast { get; set; }

    /// <summary>
    /// List of nested keys to redact (for complex values like connection strings)
    /// </summary>
    public List<string>? Keys { get; set; }
}

/// <summary>
/// Redaction modes
/// </summary>
public enum RedactionMode
{
    /// <summary>
    /// Completely replace the value with redaction characters
    /// </summary>
    Full,

    /// <summary>
    /// Show the first and last characters, redact the middle
    /// </summary>
    Partial,

    /// <summary>
    /// Apply custom redaction using RedactionOptions
    /// </summary>
    Advanced
}

/// <summary>
/// A rule for excluding configuration keys or providers
/// </summary>
public class ExclusionRule
{
    /// <summary>
    /// Regex pattern for matching configuration keys
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Regex pattern for matching the full provider name
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Regex pattern for matching provider type (class name)
    /// </summary>
    public string? ProviderType { get; set; }

    /// <summary>
    /// Regex pattern for matching provider source (file name or identifier)
    /// </summary>
    public string? ProviderSource { get; set; }
}
