# Cultiv.EnvironmentInspect &middot; [![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) [![NuGet version (Cultiv.EnvironmentInspect)](https://img.shields.io/nuget/v/Cultiv.EnvironmentInspect.svg)](https://www.nuget.org/packages/Cultiv.EnvironmentInspect/) [![Twitter](https://img.shields.io/twitter/follow/cultiv.svg?style=social&label=Follow)](https://twitter.com/intent/follow?screen_name=cultiv)


Cultiv Environment Inspector installs a dashboard in the Settings section of Umbraco, showing you the currently applied environment values and where they are coming from.

For example, we can see some of the values here are coming from `appSetting.json` and `appSetting.Development.json`. 

![Screenshot with an example of some variables, values and their sources](http://raw.githubusercontent.com/nul800sebastiaan/Cultiv.EnvironmentInspect/main/example.png)

This makes it easier to learn why some variables you expected to work have not been applied.

## Masking values from 'secret' based providers

By default, the plugin will output all values in plain text. To 'mask' the output, please add the following to the `appsettings.json` and/or `appsettings.{Environment}.json` file(s):

```json
"Cultiv": {
	"EnvironmentInspect": {
		"SecretProviders": [
			"AzureKeyVaultConfigurationProvider",
			"JsonConfigurationProvider for 'secrets.json' (Optional)"
		]
	}
}
```

In the above example, the values coming from Azure Key Vault and the local 'User Secrets' set via Visual Studio will output masked values.