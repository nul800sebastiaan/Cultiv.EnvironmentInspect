# Configuration Guide

## Configuration Reference

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Exclude` | `(string \| ExclusionRule)[]` | `[]` | Patterns or rules for keys/providers to exclude entirely |
| `Redact` | `RedactionRule[]` | `[]` | Rules for redacting sensitive values |
| `RedactionCharacter` | `string` | `•` | Character to use for redaction |
| `PartialVisibleChars` | `int` | `4` | Number of characters to show at start/end in Partial mode |
| `AzureWebAppAdvancedCopy` | `bool` | `false` | Enable Azure Web App JSON snippet copy feature in the dashboard |

### Exclusion Patterns

The `Exclude` array accepts both simple string patterns (for keys) and object rules (for provider-based exclusion). These items won't appear in the dashboard at all.

**String format** - Regex patterns for configuration keys:

```json
"Exclude": [
  "^APPSETTING_",            // Exclude environment variables starting with APPSETTING_
  "^AZURE_",                 // Exclude Azure-related environment variables
  "^EnvironmentInspect",     // Hide the EnvironmentInspect config itself
  "^\\$schema$"              // Hide the $schema property
]
```

**Object format** - Match by provider properties:

```json
"Exclude": [
  { "ProviderSource": ".*Development.*" },                    // Exclude all Development config files
  { "ProviderType": "AzureKeyVaultConfigurationProvider" },  // Exclude all Azure Key Vault values
  { "ProviderType": ".*Azure.*" }                            // Exclude all Azure-related providers
]
```

**Mixed format** - Combine both approaches:

```json
"Exclude": [
  "^APPSETTING_",
  "^\\$schema$",
  { "ProviderSource": ".*Development.*" },
  { "ProviderType": "AzureKeyVaultConfigurationProvider" }
]
```

**Exclusion Rule Properties:**

| Property | Description |
|----------|-------------|
| `Key` | Regex pattern to match configuration keys |
| `Provider` | Regex pattern to match full provider name |
| `ProviderType` | Regex pattern to match provider type (class name) |
| `ProviderSource` | Regex pattern to match provider source (file name) |

**Combining Properties** - Multiple properties can be combined using AND logic:

```json
"Exclude": [
  { "ProviderType": "JsonConfigurationProvider", "ProviderSource": ".*Development.*" }
]
```

This will exclude **only** values from `JsonConfigurationProvider` **AND** from files matching `.*Development.*` (like appsettings.Development.json). Both conditions must match for the exclusion to apply.

### Redaction Rules

The `Redact` array defines rules for masking sensitive values. Rules are processed in order, and the **first matching rule is applied**.

#### Redaction Modes

**1. Full Redaction** - Completely masks the value:
```json
{
  "Key": ".*Password$",
  "RedactionMode": "Full"
}
```
Result: `••••••••`

**2. Partial Redaction** - Shows start and end characters:
```json
{
  "Key": "Umbraco:CMS:Global:Id",
  "RedactionMode": "Partial"
}
```
Result: `2ebd••••eac5` (shows first 4 and last 4 chars by default)

**3. Advanced Redaction** - Custom redaction with options:

*Option A: Keep specific characters*
```json
{
  "Key": "Umbraco:CMS:Unattended:UnattendedUserPassword",
  "RedactionMode": "Advanced",
  "RedactionOptions": {
    "KeepFirst": 2,
    "KeepLast": 2
  }
}
```
Input: `1234567890`  
Result: `12••••90`

*Option B: Extract and redact nested keys (for connection strings)*
```json
{
  "Key": "ConnectionStrings:.*",
  "RedactionMode": "Advanced",
  "RedactionOptions": {
    "Keys": [ "Password", "PWD" ],
    "KeepFirst": 2,
    "KeepLast": 2
  }
}
```
Input: `Server=blaa.database.windows.net,1433;Database=22sa1bmi2ye;User ID=ypad0yvmodr@blaa;Password=7R9K8sS*@G2$JmpeQs($;Connection Timeout=120;`  
Result: `Server=blaa.database.windows.net,1433;Database=22sa1bmi2ye;User ID=ypad0yvmodr@blaa;Password=7R••••($;Connection Timeout=120;`

#### Provider-Based Redaction

Redact all values from specific configuration providers using provider matching properties. You can use `Provider` (full name), `ProviderType` (class name), or `ProviderSource` (file name).

**Understanding Provider Names**

ASP.NET Core configuration providers have verbose names that include the class name and additional context. Here are some real examples:

- `JsonConfigurationProvider for 'appsettings.json' (Optional)`
- `JsonConfigurationProvider for 'appsettings.Development.json' (Optional)*`
- `EnvironmentVariablesConfigurationProvider`
- `AzureKeyVaultConfigurationProvider`
- `CommandLineConfigurationProvider`

The asterisk `*` suffix indicates the active provider for a key when multiple providers define the same key.

**Matching Options**

**Option 1: Match by source file (simplest)**

```json
{
  "ProviderSource": "appsettings.Development.json",
  "RedactionMode": "Partial"
}
```

Match all development configuration files:

```json
{
  "ProviderSource": ".*Development.*",
  "RedactionMode": "Partial"
}
```

**Option 2: Match by provider type**

```json
{
  "ProviderType": "AzureKeyVaultConfigurationProvider",
  "RedactionMode": "Full"
}
```

Match all Azure-related providers:

```json
{
  "ProviderType": ".*Azure.*",
  "RedactionMode": "Full"
}
```

**Option 3: Match by full provider name (for complete control)**

```json
{
  "Provider": "JsonConfigurationProvider for 'appsettings\\.Development\\.json'.*",
  "RedactionMode": "Partial"
}
```

**Option 4: Combine multiple properties (AND logic)**

All specified properties must match:

```json
{
  "ProviderType": "JsonConfigurationProvider",
  "ProviderSource": ".*Development.*",
  "RedactionMode": "Partial"
}
```

### Visual Indicators

The dashboard displays emoji indicators for redacted values:

| Emoji | Mode | Description |
|-------|------|-------------|
| 🔒 | Full | Value is completely redacted |
| 👁️ | Partial | Value is partially visible |
| 🔐 | Advanced | Value uses custom redaction rules |

## Configuration Examples

### Basic Example

Hide internal configuration and redact all passwords:

```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^AZURE_",
      "^EnvironmentInspect",
      "^\\$schema$"
    ],
    "Redact": [
      {
        "Key": ".*Password$",
        "RedactionMode": "Full"
      }
    ]
  }
}
```

### Provider-Based Exclusion Example

Hide all values from development configuration files and Azure Key Vault:

```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^\\$schema$",
      { "ProviderSource": ".*Development.*" },
      { "ProviderType": "AzureKeyVaultConfigurationProvider" }
    ]
  }
}
```

This will completely hide:
- Any keys starting with `APPSETTING_`
- The `$schema` property
- **All configuration values from files matching `.*Development.*`** (e.g., appsettings.Development.json)
- **All configuration values from Azure Key Vault**

**Combining Properties** - Only exclude development JSON files:

```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^\\$schema$",
      { "ProviderType": "JsonConfigurationProvider", "ProviderSource": ".*Development.*" }
    ]
  }
}
```

This will hide:
- Any keys starting with `APPSETTING_`
- The `$schema` property
- **Only** values from JSON files (JsonConfigurationProvider) **AND** matching `.*Development.*`

Note: Without combining properties, `{ "ProviderSource": ".*Development.*" }` would match ANY provider with Development in the source, not just JSON files.

### Production-Ready Example

Comprehensive configuration for a production environment:

```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^AZURE_",
      "^EnvironmentInspect",
      "^\\$schema$"
    ],
    "Redact": [
      {
        "Key": "Umbraco:CMS:Unattended:UnattendedUserPassword",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": "ConnectionStrings:.*",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "Password", "PWD", "User Id", "UID" ],
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": ".*Password$",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*Secret.*",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*ApiKey.*",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*Token.*",
        "RedactionMode": "Full"
      },
      {
        "ProviderType": "AzureKeyVaultConfigurationProvider",
        "RedactionMode": "Full"
      },
      {
        "ProviderType": ".*Azure.*",
        "RedactionMode": "Full"
      }
    ],
    "RedactionCharacter": "•",
    "PartialVisibleChars": 4
  }
}
```

### Advanced Connection String Redaction

Extract and redact only sensitive parts of connection strings:

```json
{
  "EnvironmentInspect": {
    "Redact": [
      {
        "Key": "ConnectionStrings:.*",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ 
            "Password", 
            "PWD", 
            "User Id", 
            "UID",
            "password",
            "pwd",
            "user id",
            "uid"
          ],
          "KeepFirst": 3,
          "KeepLast": 3
        }
      }
    ]
  }
}
```

**Input:**
```
Server=myserver.database.windows.net;Database=mydb;User ID=myuser;Password=MyP@ssw0rd123;
```

**Output:**
```
Server=myserver.database.windows.net;Database=mydb;User ID=myu•••ser;Password=MyP•••123;
```

### Provider-Based Redaction Examples

Redact all values from specific configuration providers:

```json
{
  "EnvironmentInspect": {
    "Redact": [
      {
        "ProviderType": "AzureKeyVaultConfigurationProvider",
        "RedactionMode": "Full"
      },
      {
        "ProviderType": "AzureAppConfigurationProvider",
        "RedactionMode": "Full"
      },
      {
        "ProviderType": "EnvironmentVariablesConfigurationProvider",
        "RedactionMode": "Partial"
      },
      {
        "ProviderSource": ".*Development.*",
        "RedactionMode": "Partial"
      }
    ]
  }
}
```

### Development vs Production Configuration

**appsettings.json** (base configuration):
```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^AZURE_",
      "^EnvironmentInspect",
      "^\\$schema$"
    ],
    "RedactionCharacter": "•",
    "PartialVisibleChars": 4
  }
}
```

**appsettings.Production.json** (production overrides):
```json
{
  "EnvironmentInspect": {
    "Redact": [
      {
        "Key": ".*Password$",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*Secret.*",
        "RedactionMode": "Full"
      },
      {
        "Key": "ConnectionStrings:.*",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "Password", "PWD", "User Id" ]
        }
      }
    ]
  }
}
```

### Umbraco Cloud Configuration

Recommended configuration for Umbraco Cloud environments that protects sensitive keys commonly used in cloud deployments:

```json
{
  "EnvironmentInspect": {
    "Exclude": [
      "^APPSETTING_",
      "^AZURE_",
      "^EnvironmentInspect",
      "^\\$schema$"
    ],
    "Redact": [
      {
        "Key": "ConnectionStrings:umbracoDbDSN",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "Password", "PWD" ],
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": ".*SharedAccessSignature.*",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "sig" ],
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": ".*Secret(?!.*HeaderName).*",
        "RedactionMode": "Partial"
      },
      {
        "Key": ".*Password$",
        "RedactionMode": "Partial"
      },
      {
        "Key": "^WEBSITE_.*_KEY$",
        "RedactionMode": "Full"
      },
      {
        "Key": "Umbraco:Forms:FieldTypes:Recaptcha3:PrivateKey",
        "RedactionMode": "Partial"
      }
    ]
  }
}
```

This configuration provides:
- **Database security**: Redacts passwords in connection strings while showing server/database names
- **Azure Blob Storage**: Only redacts the signature (`sig`) in SAS tokens, keeping other parameters visible
- **Shared secrets**: Protects any configuration key containing "Secret" (except header names like `SHAREDSECRET:HEADERNAME`)
- **Website keys**: Fully hides Azure App Service authentication keys (`WEBSITE_AUTH_ENCRYPTION_KEY`, `WEBSITE_AUTH_SIGNING_KEY`)
- **Umbraco Forms**: Protects reCAPTCHA private keys with partial visibility

## Dashboard Features

### Copy to Clipboard

The dashboard provides copy buttons for quick clipboard access:

- **Copy Key**: Click the 📋 button next to any key to copy it (respects the "Environment variable format" toggle)
- **Copy Value**: Click the 📋 button next to any value to copy it
- All copy operations show a success/failure notification

### Azure Web App JSON Export

Enable the Azure Web App advanced copy feature to export configuration as Azure-ready JSON snippets:

```json
{
  "EnvironmentInspect": {
    "AzureWebAppAdvancedCopy": true
  }
}
```

When enabled, an additional "Azure" column appears with a ☁️ button. Clicking it copies a JSON snippet formatted for Azure Web App configuration:

```json
{
  "name": "UMBRACO__CMS__GLOBAL__ID",
  "value": "2ebd3039-ba1b-4d64-8678-d13accaeeac5",
  "slotSetting": false
}
```

This format can be directly imported into:
- Azure Portal → Configuration → Application settings
- Azure CLI → `az webapp config appsettings set`
- ARM templates
- Bicep files

### Filter Options

The dashboard includes toggles for real-time filtering:

- **Exclude empty values** (default: on) - Hide entries with null or empty values
- **Only redacted** - Show only values that have been redacted
- **Environment variable format** - Convert `:` to `__` in all keys (e.g., `Umbraco:CMS:Global` → `UMBRACO__CMS__GLOBAL`)

## Regex Pattern Tips

### Common Patterns

- `^APPSETTING_` - Starts with "APPSETTING_"
- `.*Password$` - Ends with "Password"
- `.*Secret.*` - Contains "Secret"
- `^ConnectionStrings:` - Starts with "ConnectionStrings:"
- `Umbraco:CMS:.*:.*Password` - Umbraco CMS passwords at any depth

### Escaping Special Characters

In JSON, backslashes must be escaped:

- `.` → `\\.` (literal dot)
- `$` → `\\$` (literal dollar sign)
- `^` → `^` (start of string - no escape needed)

### Testing Regex

Use [regex101.com](https://regex101.com/) with the "ECMAScript (JavaScript)" flavor to test your patterns.

## Rule Priority

Redaction rules are processed **in order**, and the **first matching rule is applied**. Place more specific rules before general ones:

**✅ Correct Order:**
```json
{
  "Redact": [
    {
      "Key": "Umbraco:CMS:Unattended:UnattendedUserPassword",
      "RedactionMode": "Partial"
    },
    {
      "Key": ".*Password$",
      "RedactionMode": "Full"
    }
  ]
}
```

**❌ Incorrect Order:**
```json
{
  "Redact": [
    {
      "Key": ".*Password$",
      "RedactionMode": "Full"
    },
    {
      "Key": "Umbraco:CMS:Unattended:UnattendedUserPassword",
      "RedactionMode": "Partial"
    }
  ]
}
```

In the incorrect example, the specific Umbraco password rule will never be applied because `.*Password$` matches first.

## Visual Indicators

The dashboard displays emoji indicators for redacted values:

| Emoji | Mode | Description |
|-------|------|-------------|
| 🔒 | Full | Value is completely redacted |
| 👁️ | Partial | Value is partially visible |
| 🔐 | Advanced | Value uses custom redaction rules |

## Best Practices

1. **Test in Development**: Verify your exclusion and redaction rules work as expected before deploying to production
2. **Document Your Patterns**: Add comments in your configuration to explain complex regex patterns
3. **Review Regularly**: As your application grows, review and update your configuration rules
4. **Use Environment-Specific Files**: Apply stricter redaction in production environments
5. **Monitor Performance**: If you have thousands of configuration keys, use exclusion patterns to reduce the dataset
6. **Validate Regex**: Invalid regex patterns will be logged as warnings but won't break the application

## Troubleshooting

### Rule Not Matching

- Check regex syntax - use [regex101.com](https://regex101.com/)
- Remember to escape backslashes in JSON: `\.` becomes `\\.`
- Check rule order - a previous rule may be matching first

### Too Much/Too Little Visible

Adjust the `PartialVisibleChars` setting or use `Advanced` mode with `KeepFirst`/`KeepLast` options.

### Performance Issues

- Use `Exclude` patterns to remove large sections of configuration
- Use the "Exclude empty values" toggle in the dashboard UI to filter out empty entries
- Cache is automatically cleared when configuration changes

## Support

For issues, questions, or contributions, visit the [GitHub repository](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect).
