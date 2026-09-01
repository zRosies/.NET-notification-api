using MyApi.Models;

namespace MyApi.Services;

public interface INotificationService
{
    IReadOnlyList<string> SupportedTypes { get; }
    Task<List<Notification>> GetAllAsync();
    Task<Notification?> GetByIdAsync(Guid id);
    Task<Notification> CreateAsync(CreateNotificationRequest request);
    Task<Notification?> UpdateAsync(Guid id, UpdateNotificationRequest request);
    Task<bool> DeleteAsync(Guid id);
    bool IsSupportedType(string type);
}
