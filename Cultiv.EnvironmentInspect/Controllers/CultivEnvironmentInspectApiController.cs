using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Web.Common.Authorization;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Cultiv.EnvironmentInspect")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    public class CultivEnvironmentInspectApiController : CultivEnvironmentInspectApiControllerBase
    {
        private readonly IConfiguration _configuration;

        public CultivEnvironmentInspectApiController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("ping")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string Ping() => "Pong";


        [HttpGet("getenvironment")]
        [MapToApiVersion("1.0")]
        public List<DebugViewModel> GetEnvironment()
        {
            var debugViewModel = new List<DebugViewModel>();

            if (_configuration is not IConfigurationRoot configurationRoot) return debugViewModel;

            // Adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Abstractions/src/ConfigurationRootExtensions.cs#L36
            void RecurseChildren(IEnumerable<IConfigurationSection> children)
            {
                foreach (var child in children)
                {
                    var valueAndProvider = GetValueAndProvider(configurationRoot, child.Path);

                    if (valueAndProvider.Provider != null)
                    {
                        debugViewModel.Add(new DebugViewModel
                        {
                            Key = child.Path,
                            Value = valueAndProvider.Value,
                            Provider = valueAndProvider.Provider.ToString(),
                        });
                    }
                    else
                    {
                        debugViewModel.Add(new DebugViewModel
                        {
                            Key = child.Path
                        });
                    }

                    RecurseChildren(child.GetChildren());
                }
            }

            RecurseChildren(configurationRoot.GetChildren().Where(x => !string.IsNullOrEmpty(x.Path)));

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
