using System.Collections.Generic;
using System.Linq;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.Authorization;

namespace Cultiv.EnvironmentInspect.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [VersionedApiBackOfficeRoute("environmentinspect")]
    [ApiExplorerSettings(GroupName = "Cultiv.EnvironmentInspect")]
    [ApiVersion("1.0")]
    [MapToApi("environmentinspect-api")]
    public class EnvironmentController : ManagementApiControllerBase
    {
        private readonly IConfiguration _configuration;

        public EnvironmentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

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

            RecurseChildren(configurationRoot.GetChildren().Where(x => !string.IsNullOrEmpty(x.Path) ));

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

    public class EnvironmentInspectApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.ConfigureOptions<EnvironmentInspectApiSwaggerGenOptions>();
        }
    }

    public class EnvironmentInspectApiSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
    {
        public void Configure(SwaggerGenOptions options)
        {
            options.SwaggerDoc(
                "environmentinspect-api",
                new OpenApiInfo { Title = "Cultiv.EnvironmentInspect", Version = "1.0" }
            );

            options.OperationFilter<EnvironmentInspectApiSwaggerGenOptionsApiOperationSecurityFilter>();
        }
    }

    public class EnvironmentInspectApiSwaggerGenOptionsApiOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => "environmentinspect-api";
    }
}