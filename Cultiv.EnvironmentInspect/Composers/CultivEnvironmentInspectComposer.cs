using Cultiv.EnvironmentInspect.Configuration;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Cultiv.EnvironmentInspect.Composers
{
    public class CultivEnvironmentInspectComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddScoped<IEnvironmentInspectService, EnvironmentInspectService>();
            
            builder.Services.AddOptions<EnvironmentInspectOptions>()
                .Configure<IConfiguration>((options, configuration) =>
                {
                    var section = configuration.GetSection("EnvironmentInspect");
                    
                    // Bind everything except Exclude first
                    section.Bind(options);
                    
                    // Manually bind Exclude array to handle string and object formats
                    var excludeSection = section.GetSection("Exclude");
                    if (excludeSection.Exists())
                    {
                        options.Exclude.Clear();
                        foreach (var child in excludeSection.GetChildren())
                        {
                            var value = child.Get<string>();
                            if (!string.IsNullOrEmpty(value))
                            {
                                // String format - treat as Key pattern
                                options.Exclude.Add(new ExclusionRule { Key = value });
                            }
                            else
                            {
                                // Object format - bind all properties
                                var rule = new ExclusionRule();
                                child.Bind(rule);
                                options.Exclude.Add(rule);
                            }
                        }
                    }
                });
        }
    }
}
