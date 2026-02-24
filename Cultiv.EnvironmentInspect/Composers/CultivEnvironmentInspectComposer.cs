using System.Linq;
using Cultiv.EnvironmentInspect.Configuration;
using Cultiv.EnvironmentInspect.NotificationHandlers;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Cultiv.EnvironmentInspect.Composers
{
    public class CultivEnvironmentInspectComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddSingleton<IEnvironmentInspectService, EnvironmentInspectService>();
            
            // Register configuration options
            builder.Services.Configure<EnvironmentInspectOptions>(
                builder.Config.GetSection("EnvironmentInspect"));
            
            // Post-configure to manually bind the Exclude array (runs on every CurrentValue access)
            builder.Services.PostConfigure<EnvironmentInspectOptions>(options =>
            {
                var config = builder.Config;
                var excludeSection = config.GetSection("EnvironmentInspect:Exclude");
                
                if (excludeSection.Exists())
                {
                    options.Exclude.Clear();
                    foreach (var child in excludeSection.GetChildren())
                    {
                        // Check if it's a simple value (string) or a complex object
                        if (!string.IsNullOrEmpty(child.Value) && !child.GetChildren().Any())
                        {
                            // It's a string value
                            options.Exclude.Add(new ExclusionRule { Key = child.Value });
                        }
                        else
                        {
                            // It's an object with properties
                            var rule = new ExclusionRule();
                            child.Bind(rule);
                            options.Exclude.Add(rule);
                        }
                    }
                }
            });
            
            // Pre-warm the cache on application startup
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, CachePrewarmHandler>();
        }
    }
}
