namespace MyApi.Models;

public record CreateNotificationRequest(
    string Type,
    string Title,
    string Message,
    string RecipientId,
    string? EntityId = null,
    string? Metadata = null);

public record UpdateNotificationRequest(
    string Type,
    string Title,
    string Message,
    string RecipientId,
    bool IsRead,
    string? EntityId = null,
    string? Metadata = null);