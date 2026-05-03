using Asp.Versioning;
using Cultiv.EnvironmentInspect.BackgroundJobs;
using Cultiv.EnvironmentInspect.Configuration;
using Cultiv.EnvironmentInspect.Data;
using Cultiv.EnvironmentInspect.Migrations;
using Cultiv.EnvironmentInspect.NotificationHandlers;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Cultiv.EnvironmentInspect.Composers
{
    public class CultivEnvironmentInspectComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            // Register configuration options with post-configuration to handle hybrid Exclude array
            builder.Services.Configure<EnvironmentInspectOptions>(
                builder.Config.GetSection("EnvironmentInspect"));

            // Post-configure to manually bind the Exclude array
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

            // Register the environment inspect service
            builder.Services.AddSingleton<IEnvironmentInspectService, EnvironmentInspectService>();

            // Register user preferences service
            builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();

            // Register EF Core DbContext with Umbraco's database connection
            builder.Services.AddDbContext<EnvironmentInspectDbContext>((serviceProvider, options) =>
            {
                var connectionStrings = serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>().CurrentValue;

                var connectionString = connectionStrings.ConnectionString!;
                var providerName = connectionStrings.ProviderName;

                // Resolve |DataDirectory| placeholder for SQLite
                if (connectionString.Contains("|DataDirectory|", StringComparison.OrdinalIgnoreCase))
                {
                    var hostEnvironment = serviceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                    var dataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "umbraco", "Data");
                    connectionString = connectionString.Replace("|DataDirectory|", dataDirectory, StringComparison.OrdinalIgnoreCase);
                }

                if (string.IsNullOrWhiteSpace(providerName) || providerName.Contains("SQLite", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseSqlite(connectionString);
                }
                else
                {
                    options.UseSqlServer(connectionString);
                }

                // Suppress pending model changes warning - necessary for cross-database compatibility
                // The migration uses SQL Server types (nvarchar, datetime2) which work on both providers
                // but SQLite sees them as mismatches since it expects TEXT/INTEGER types
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Register operation ID handler for Swagger
            builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

            // Pre-warm the cache on application startup
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, CachePrewarmHandler>();

            // Run database migrations on application startup
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunUserPreferencesMigration>();

            // Register weekly cleanup job for orphaned user preferences (distributed - runs on one server only)
            builder.Services.AddSingleton<IDistributedBackgroundJob, UserPreferencesCleanupJob>();

            builder.Services.Configure<SwaggerGenOptions>(opt =>
            {
                // Related documentation:
                // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api
                // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/adding-a-custom-swagger-document
                // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/versioning-your-api
                // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/access-policies

                // Configure the Swagger generation options
                // Add in a new Swagger API document solely for our own package that can be browsed via Swagger UI
                // Along with having a generated swagger JSON file that we can use to auto generate a TypeScript client
                opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
                {
                    Title = "Cultiv Environment Inspect Backoffice API",
                    Version = "1.0",
                    // Contact = new OpenApiContact
                    // {
                    //     Name = "Some Developer",
                    //     Email = "you@company.com",
                    //     Url = new Uri("https://company.com")
                    // }
                });

                // Enable Umbraco authentication for the "Example" Swagger document
                // PR: https://github.com/umbraco/Umbraco-CMS/pull/15699
                opt.OperationFilter<CultivEnvironmentInspectOperationSecurityFilter>();
            });
        }

        public class CultivEnvironmentInspectOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
        {
            protected override string ApiName => Constants.ApiName;
        }

        // This is used to generate nice operation IDs in our swagger json file
        // So that the gnerated TypeScript client has nice method names and not too verbose
        // https://docs.umbraco.com/umbraco-cms/tutorials/creating-a-backoffice-api/umbraco-schema-and-operation-ids#operation-ids
        public class CustomOperationHandler : OperationIdHandler
        {
            public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
            {
            }

            protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            {
                return controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("Cultiv.EnvironmentInspect.Controllers", comparisonType: StringComparison.InvariantCultureIgnoreCase) is true;
            }

            public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
        }
    }
}
