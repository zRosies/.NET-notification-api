namespace MyApi.Models;

public record CreateNotificationRequest(
    string Type,
    string Title,
    string Message,
    string RecipientId);

public record UpdateNotificationRequest(
    string Type,
    string Title,
    string Message,
    string RecipientId,
    bool IsRead);