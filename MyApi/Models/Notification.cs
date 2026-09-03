namespace MyApi.Models;

public enum NotificationType
{
    ORDER_CREATED,
    PAYMENT_SUCCEEDED,
    PAYMENT_FAILED
}

public class Notification
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? EntityId { get; set; }
    public string? Metadata { get; set; }
}