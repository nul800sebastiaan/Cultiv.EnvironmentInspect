using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Cultiv.EnvironmentInspect.Services;

namespace Cultiv.EnvironmentInspect.NotificationHandlers
{
    public class CachePrewarmHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
    {
        private readonly IEnvironmentInspectService _environmentService;

        public CachePrewarmHandler(IEnvironmentInspectService environmentService)
        {
            _environmentService = environmentService;
        }

        public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        {
            // Pre-warm the cache in the background without blocking startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await _environmentService.GetEnvironmentDataAsync();
                }
                catch
                {
                    // Silently ignore errors during pre-warming
                }
            }, cancellationToken);
        }
    }
}