using MyApi.Models;

namespace MyApi.Services;

public class CompositeNotificationProvider(IEnumerable<INotificationProvider> providers) : INotificationProvider
{
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers)
        {
            try
            {
                await provider.SendAsync(notification, cancellationToken);
            }
            catch (Exception exception)
            {   
                Console.Error.WriteLine($"Failed to send notification via {provider.GetType().Name} for notification {notification.Id}: {exception}");
            }
        }
    }
}
