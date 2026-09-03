using MyApi.Models;

namespace MyApi.Services;

public interface INotificationProvider
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
