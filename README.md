# Cultiv.EnvironmentInspect &middot; [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) [![NuGet version (Cultiv.EnvironmentInspect)](https://img.shields.io/nuget/v/Cultiv.EnvironmentInspect.svg)](https://www.nuget.org/packages/Cultiv.EnvironmentInspect/) [![CI](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/actions/workflows/ci.yml/badge.svg)](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/actions)

**v1 is for Umbraco v9 to v13**  
**v2 is for Umbraco v17**  
**v3 is for Umbraco v18+**

Cultiv Environment Inspector installs a dashboard in the Settings section of Umbraco, showing you the currently applied environment values and where they are coming from.

For example, we can see some of the values here are coming from `appsetting.json` and from Umbraco Cloud Environment variables. 

![Screenshot with an example of some variables, values and their sources](http://raw.githubusercontent.com/nul800sebastiaan/Cultiv.EnvironmentInspect/develop/v2/example.png)

This makes it easier to learn why some variables you expected to work have not been applied.

## Features

- 📊 **View all configuration values** and their sources in the Umbraco backoffice
- ⭐ **Star favorites** - Pin important configuration keys to the top for quick access (per-user preference)
- 🔍 **Search filter** - Filter configuration entries by key, value, or provider name
- 🔒 **Redact sensitive data** with multiple redaction modes (Full, Partial, Advanced)
- 🎯 **Exclude configuration keys** using regex patterns
- 🔐 **Provider-based filtering** - match by provider type, source file, or full name
- 📋 **Copy keys and values** with one-click clipboard support and notifications
- ☁️ **Azure Web App JSON export** - copy configuration as ready-to-use Azure Web App JSON snippets
- 🔄 **Environment variable format** - convert keys from `Umbraco:CMS:Setting` to `UMBRACO__CMS__SETTING`
- 👤 **Per-user preferences** - Display settings, filters, and starred items are saved per user
- 🚀 **Performance optimized** with runtime caching and change detection
- 🔄 **Hot reload support** - configuration changes are automatically applied

## Installation

Install via NuGet:

```bash
dotnet add package Cultiv.EnvironmentInspect
```

Or via the NuGet Package Manager Console:

```powershell
Install-Package Cultiv.EnvironmentInspect
```

## Usage

1. Install the package
2. Access the dashboard in **Settings → Environment Inspector**
3. View all configuration values and their sources

That's it! The package works out of the box with no configuration required.

### Dashboard Features

- **Star favorites** ⭐ - Mark important configuration keys as favorites to keep them pinned at the top of the list (per-user preference)
- **Search filter** 🔍 - Type to filter configuration entries by key, value, or provider name in real-time
- **Settings popover** ⚙️ - Access display settings including:
  - Environment variable format toggle (`Umbraco:CMS:Setting` ↔ `UMBRACO__CMS__SETTING`)
  - Show/hide Azure column (when `AzureWebAppAdvancedCopy` is enabled)
- **Filter toggles** - Quickly filter to:
  - Exclude empty values
  - Show only redacted items
  - Show only starred favorites
- **Copy buttons** - One-click clipboard copy for keys and values with success notifications
- **Azure Web App export** ☁️ - Enable `AzureWebAppAdvancedCopy` to get a button that copies Azure-ready JSON snippets
- **Virtual scrolling** - Efficiently handles thousands of configuration entries
- **Per-user preferences** - All display settings, filters, and starred items are saved per user

## Configuration (Optional)

If you need to exclude or redact sensitive values, add the `EnvironmentInspect` section to your `appsettings.json`:

```json
{
  "EnvironmentInspect": {
    "AzureWebAppAdvancedCopy": true,
    "Exclude": [
      "^APPSETTING_",
      "^AZURE_",
      "^EnvironmentInspect",
      "^\\$schema$"
    ],
    "Redact": [
      {
        "Key": "ConnectionStrings:.*",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "Password", "PWD" ],
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": "Umbraco:Storage:AzureBlob:Media:ConnectionString",
        "RedactionMode": "Advanced",
        "RedactionOptions": {
          "Keys": [ "AccountKey" ],
          "KeepFirst": 2,
          "KeepLast": 2
        }
      },
      {
        "Key": "^WEBSITE_.*_KEY$",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*Password$",
        "RedactionMode": "Full"
      },
      {
        "Key": ".*Secret(?!.*HeaderName).*",
        "RedactionMode": "Partial"
      }
    ]
  }
}
```

**Results:**
- Connection string password: `7R••••($` (extracted from connection string) 🔐
- Azure Blob Storage AccountKey: `hG••••9k` (extracted from connection string) 🔐
- Keys matching `^WEBSITE_.*_KEY$`: `••••••••` 🔒
- Keys matching `.*Password$`: `••••••••` 🔒
- Keys matching `.*Secret(?!.*HeaderName).*`: `S3cr••••XyZ` 👁️

For detailed configuration options, see the [Configuration Guide](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v2/CONFIGURATION.md).

### Umbraco Cloud

For Umbraco Cloud deployments, see the [Umbraco Cloud configuration example](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v2/CONFIGURATION.md#umbraco-cloud-configuration) which includes recommended redaction rules for:
- Azure Blob Storage SAS tokens
- Website authentication keys
- Database connection strings
- Umbraco Forms reCAPTCHA keys

## Documentation

- **[Configuration Guide](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v2/CONFIGURATION.md)** - Detailed configuration options, examples, and best practices

## Best Practices

1. **Order matters**: Place specific redaction rules before general ones
2. **Test regex patterns**: Use [regex101.com](https://regex101.com/) to validate your patterns
3. **Use Advanced mode for connection strings**: Extract and redact only the sensitive parts

## License

MIT License - see [LICENSE](LICENSE) file for details.