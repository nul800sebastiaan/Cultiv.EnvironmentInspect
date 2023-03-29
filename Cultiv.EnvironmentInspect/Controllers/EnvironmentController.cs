using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Authorization;

namespace Cultiv.EnvironmentInspect.Site
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class EnvironmentController : UmbracoAuthorizedJsonController
    {
        private readonly IConfiguration _configuration;

        public EnvironmentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public List<DebugViewModel> GetEnvironment()
        {
            var debugViewModel = new List<DebugViewModel>();

            if (_configuration is not IConfigurationRoot configurationRoot) return debugViewModel;

			// Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Abstractions/src/ConfigurationRootExtensions.cs#L36
			void RecurseChildren(List<string> secretProviders, IEnumerable<IConfigurationSection> children)
            {
                foreach (var child in children)
                {
                    var valueAndProvider = GetValueAndProvider(configurationRoot, child.Path);

                    if (valueAndProvider.Provider != null)
                    {
                        debugViewModel.Add(new DebugViewModel
                        {
                            Key = child.Path,
                            Value = secretProviders.Contains(valueAndProvider.Provider.ToString()) ? "Masked ******" : valueAndProvider.Value,
                            Provider = valueAndProvider.Provider.ToString()
                        });
                    }
                    else
                    {
                        debugViewModel.Add(new DebugViewModel
                        {
                            Key = child.Path
                        });
                    }

                    RecurseChildren(secretProviders, child.GetChildren());
                }
            }

            var secretProviders = _configuration.GetSection("Cultiv:EnvironmentInspect:SecretProviders").Get<List<string>>() ?? new List<string>();

            RecurseChildren(secretProviders, configurationRoot.GetChildren());

            return debugViewModel;
        }
        private static (string Value, IConfigurationProvider Provider) GetValueAndProvider(
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
        
        public class DebugViewModel
        {
            public string Key { get; set; }
            public string Value { get; set; }
            public string Provider { get; set; }
        }
    }
}