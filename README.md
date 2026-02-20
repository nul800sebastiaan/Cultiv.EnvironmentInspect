# Cultiv.EnvironmentInspect &middot; [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) [![NuGet version (Cultiv.EnvironmentInspect)](https://img.shields.io/nuget/v/Cultiv.EnvironmentInspect.svg)](https://www.nuget.org/packages/Cultiv.EnvironmentInspect/) [![CI](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/actions/workflows/ci.yml/badge.svg)](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/actions)

**v1 is for Umbraco v9 to v13**  
**v2 is for Umbraco v17+**

> **This is the v1 branch targeting Umbraco v9-v13. For Umbraco v17+, see the [v2 branch](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/tree/develop/v2).**

Cultiv Environment Inspector installs a dashboard in the Settings section of Umbraco, showing you the currently applied environment values and where they are coming from.

For example, we can see some of the values here are coming from `appsetting.json` and from Umbraco Cloud Environment variables. 

![Screenshot with an example of some variables, values and their sources](http://raw.githubusercontent.com/nul800sebastiaan/Cultiv.EnvironmentInspect/develop/v1/example.png)

This makes it easier to learn why some variables you expected to work have not been applied.

## Features

- 📊 **View all configuration values** and their sources in the Umbraco backoffice
- 🔒 **Redact sensitive data** with multiple redaction modes (Full, Partial, Advanced)
- 🎯 **Exclude configuration keys** using regex patterns
- 🔐 **Provider-based filtering** - match by provider type, source file, or full name
- 📋 **Copy keys and values** with one-click clipboard support and notifications
- ☁️ **Azure Web App JSON export** - copy configuration as ready-to-use Azure Web App JSON snippets
- 🔄 **Environment variable format** - convert keys from `Umbraco:CMS:Setting` to `UMBRACO__CMS__SETTING`
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

- **Copy buttons** - One-click clipboard copy for keys and values with success notifications
- **Filter options** - Toggle to exclude empty values, show only redacted items, or convert keys to environment variable format
- **Azure Web App export** - Enable `AzureWebAppAdvancedCopy` to get a ☁️ button that copies Azure-ready JSON snippets

## Configuration (Optional)

If you need to exclude or redact sensitive values, add the `EnvironmentInspect` section to your `appsettings.json`:

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
          "Keys": [ "Password", "PWD" ],
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
        "ProviderType": "AzureKeyVaultConfigurationProvider",
        "RedactionMode": "Full"
      }
    ],
    "RedactionCharacter": "•",
    "PartialVisibleChars": 4,
    "AzureWebAppAdvancedCopy": false
  }
}
```

**Results:**
- `UnattendedUserPassword`: `12••••90` 🔐
- Connection string password: `7R••••($` (extracted from connection string) 🔐
- Keys matching `.*Password$`: `••••••••` 🔒
- Keys matching `.*Secret.*`: `••••••••` 🔒

For detailed configuration options, see the [Configuration Guide](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v1/CONFIGURATION.md).

### Umbraco Cloud

For Umbraco Cloud deployments, see the [Umbraco Cloud configuration example](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v1/CONFIGURATION.md#umbraco-cloud-configuration) which includes recommended redaction rules for:
- Azure Blob Storage SAS tokens
- Website authentication keys
- Database connection strings
- Umbraco Forms reCAPTCHA keys

## Documentation

- **[Configuration Guide](https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v1/CONFIGURATION.md)** - Detailed configuration options, examples, and best practices

## Best Practices

1. **Order matters**: Place specific redaction rules before general ones
2. **Test regex patterns**: Use [regex101.com](https://regex101.com/) to validate your patterns
3. **Use Advanced mode for connection strings**: Extract and redact only the sensitive parts

## License

MIT License - see [LICENSE](LICENSE) file for details.
