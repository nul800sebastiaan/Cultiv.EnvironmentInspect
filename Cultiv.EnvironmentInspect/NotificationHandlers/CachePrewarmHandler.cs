using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Cultiv.EnvironmentInspect.Services;
using Microsoft.Extensions.Logging;

namespace Cultiv.EnvironmentInspect.NotificationHandlers
{
    public class CachePrewarmHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
    {
        private readonly IEnvironmentInspectService _environmentService;
        private readonly ILogger<CachePrewarmHandler> _logger;

        public CachePrewarmHandler(IEnvironmentInspectService environmentService, ILogger<CachePrewarmHandler> logger)
        {
            _environmentService = environmentService;
            _logger = logger;
        }

        public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        {
            // Pre-warm the cache in the background without blocking startup
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Pre-warming environment inspect cache...");
                    await _environmentService.GetEnvironmentDataAsync();
                    _logger.LogInformation("Environment inspect cache pre-warmed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pre-warm environment inspect cache");
                }
            }, cancellationToken);
        }
    }
}