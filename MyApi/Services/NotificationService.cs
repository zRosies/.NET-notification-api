using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Models;

namespace MyApi.Services;

public class NotificationService(NotificationsDbContext db) : INotificationService
{
    private static readonly string[] supportedTypes =
        ["payment_succeeded", "order_created", "payment_failed"];

    public IReadOnlyList<string> SupportedTypes => supportedTypes;

    public async Task<List<Notification>> GetAllAsync()
    {
        return await db.Notifications
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Notification?> GetByIdAsync(Guid id)
    {
        return await db.Notifications.FindAsync(id);
    }

    public async Task<Notification> CreateAsync(CreateNotificationRequest request)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = request.Type.ToLowerInvariant(),
            Title = request.Title,
            Message = request.Message,
            RecipientId = request.RecipientId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }

    public async Task<Notification?> UpdateAsync(Guid id, UpdateNotificationRequest request)
    {
        var notification = await db.Notifications.FindAsync(id);
        if (notification is null)
        {
            return null;
        }

        notification.Type = request.Type.ToLowerInvariant();
        notification.Title = request.Title;
        notification.Message = request.Message;
        notification.RecipientId = request.RecipientId;
        notification.IsRead = request.IsRead;
        await db.SaveChangesAsync();
        return notification;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var notification = await db.Notifications.FindAsync(id);
        if (notification is null)
        {
            return false;
        }

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
        return true;
    }

    public bool IsSupportedType(string type) =>
        supportedTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
}
