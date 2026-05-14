using Cultiv.EnvironmentInspect.BackgroundJobs;
using Cultiv.EnvironmentInspect.Configuration;
using Cultiv.EnvironmentInspect.Data;
using Cultiv.EnvironmentInspect.Migrations;
using Cultiv.EnvironmentInspect.NotificationHandlers;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
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

            // Pre-warm the cache on application startup
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, CachePrewarmHandler>();

            // Run database migrations on application startup
            builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunUserPreferencesMigration>();

            // Register weekly cleanup job for orphaned user preferences (distributed - runs on one server only)
            builder.Services.AddSingleton<IDistributedBackgroundJob, UserPreferencesCleanupJob>();

            // Register the OpenAPI document for this package
            builder.AddBackOfficeOpenApiDocument(
                Constants.ApiName,
                document => document
                    .WithTitle("Cultiv Environment Inspect Backoffice API")
                    .WithBackOfficeAuthentication()
                    .ConfigureOpenApiOptions(options =>
                    {
                        options.AddOperationTransformer<CustomOperationIdTransformer>();
                    }));
        }

        // Generates concise operation IDs (just the action name) for our controllers
        private sealed class CustomOperationIdTransformer : IOpenApiOperationTransformer
        {
            public Task TransformAsync(
                OpenApiOperation operation,
                OpenApiOperationTransformerContext context,
                CancellationToken cancellationToken)
            {
                if (context.Description.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor &&
                    controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(
                        "Cultiv.EnvironmentInspect.Controllers",
                        StringComparison.InvariantCultureIgnoreCase) is true)
                {
                    operation.OperationId = $"{context.Description.ActionDescriptor.RouteValues["action"]}";
                }

                return Task.CompletedTask;
            }
        }
    }
}
