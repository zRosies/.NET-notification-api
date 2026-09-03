using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Models;
using System.Text.Json;

namespace MyApi.Services;

public class NotificationService(NotificationsDbContext db, INotificationProvider notificationProvider) : INotificationService
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
        var notificationType = ParseType(request.Type);
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = notificationType,
            Title = request.Title,
            Message = request.Message,
            RecipientId = request.RecipientId,
            EntityId = request.EntityId,
            Metadata = request.Metadata,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        await notificationProvider.SendAsync(notification);
        return notification;
    }

    public async Task<Notification> CreateFromEventAsync(NotificationType type, string recipientId, string? entityId, string? metadataJson, string? title = null, string? message = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = title ?? MapTitle(type),
            Message = message ?? MapMessage(type, entityId),
            RecipientId = recipientId,
            EntityId = entityId,
            Metadata = metadataJson ?? "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        await notificationProvider.SendAsync(notification);
        return notification;
    }

    public async Task<Notification?> UpdateAsync(Guid id, UpdateNotificationRequest request)
    {
        var notification = await db.Notifications.FindAsync(id);
        if (notification is null)
        {
            return null;
        }

        notification.Type = ParseType(request.Type);
        notification.Title = request.Title;
        notification.Message = request.Message;
        notification.RecipientId = request.RecipientId;
        notification.EntityId = request.EntityId;
        notification.Metadata = request.Metadata;
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
        Enum.TryParse<NotificationType>(NormalizeType(type), true, out _);

    private static NotificationType ParseType(string type)
    {
        if (Enum.TryParse<NotificationType>(NormalizeType(type), true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Unsupported notification type '{type}'. Supported values: {string.Join(", ", supportedTypes)}");
    }

    private static string NormalizeType(string type)
    {
        return type.Replace("-", "_").Replace(" ", "_").Trim();
    }

    private static string MapTitle(NotificationType type) => type switch
    {
        NotificationType.ORDER_CREATED => "Order created",
        NotificationType.PAYMENT_SUCCEEDED => "Payment succeeded",
        NotificationType.PAYMENT_FAILED => "Payment failed",
        _ => "Notification"
    };

    private static string MapMessage(NotificationType type, string? entityId) => type switch
    {
        NotificationType.ORDER_CREATED => $"Order {entityId ?? "unknown"} was created.",
        NotificationType.PAYMENT_SUCCEEDED => $"Payment {entityId ?? "unknown"} succeeded.",
        NotificationType.PAYMENT_FAILED => $"Payment {entityId ?? "unknown"} failed.",
        _ => "A notification was created."
    };
}
