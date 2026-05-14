using Cultiv.EnvironmentInspect.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Cultiv.EnvironmentInspect.NotificationHandlers
{
    public class CachePrewarmHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
    {
        private readonly IEnvironmentInspectService _environmentService;
        private readonly IRuntimeState _runtimeState;
        private readonly ILogger<CachePrewarmHandler> _logger;

        public CachePrewarmHandler(IEnvironmentInspectService environmentService, IRuntimeState runtimeState, ILogger<CachePrewarmHandler> logger)
        {
            _environmentService = environmentService;
            _runtimeState = runtimeState;
            _logger = logger;
        }

        public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        {
            if (_runtimeState.Level != RuntimeLevel.Run)
            {
                return;
            }

            // Pre-warm the cache in the background without blocking startup
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Pre-warming environment inspect cache...");
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
