using System.Text;
using System.Text.Json;
using MyApi.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MyApi.Services;

public class RabbitMqNotificationConsumerService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<RabbitMqNotificationConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var orderQueue = configuration["RABBITMQ_ORDER_QUEUE"] ?? "order_events";
        var paymentQueue = configuration["RABBITMQ_PAYMENT_QUEUE"] ?? "payment_events";
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration["RABBITMQ_URL"] ?? "amqp://guest:guest@localhost:5672")
        };

        logger.LogInformation("Connecting to RabbitMQ. Order queue: {OrderQueue}; Payment queue: {PaymentQueue}.", orderQueue, paymentQueue);
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        var orderChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        var paymentChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await orderChannel.QueueDeclareAsync(queue: orderQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
        await paymentChannel.QueueDeclareAsync(queue: paymentQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        await ConsumeAsync(orderChannel, orderQueue, isOrderQueue: true, stoppingToken);
        await ConsumeAsync(paymentChannel, paymentQueue, isOrderQueue: false, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ConsumeAsync(IChannel channel, string queueName, bool isOrderQueue, CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var payload = Encoding.UTF8.GetString(body);
                var envelope = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload);
                var eventPattern = GetString(envelope, "pattern") ?? GetString(envelope, "event") ?? "direct-payload";
                logger.LogInformation("RabbitMQ event received. Queue: {QueueName}; Pattern: {EventPattern}; Raw payload: {Payload}", queueName, eventPattern, payload);
                var message = UnwrapEventData(envelope);

                if (message is null)
                {
                    throw new JsonException($"The {queueName} message is empty or is not a JSON object.");
                }

                logger.LogInformation("Received {QueueName} notification event with {ItemCount} item(s).", queueName, GetItemCount(message));
                logger.LogInformation("Normalized RabbitMQ payload from {QueueName}, pattern {EventPattern}: {Payload}", queueName, eventPattern, JsonSerializer.Serialize(message));

                using var scope = serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                if (isOrderQueue)
                {
                    var userId = GetString(message, "userId") ?? GetString(message, "customerId") ?? "unknown";
                    var orderId = GetString(message, "orderId") ?? GetString(message, "id") ?? Guid.NewGuid().ToString();
                    var metadata = BuildMetadata(message);

                    await notificationService.CreateFromEventAsync(
                        NotificationType.ORDER_CREATED,
                        userId,
                        orderId,
                        metadata,
                        "New order created",
                        BuildOrderMessage(message, orderId));
                }
                    else
                {
                    var eventType = GetString(message, "eventType") ?? GetString(message, "type") ?? "unknown";
                    var userId = GetString(message, "userId") ?? "unknown";
                    var paymentId = GetString(message, "paymentId") ?? GetString(message, "id") ?? Guid.NewGuid().ToString();
                    var metadata = BuildMetadata(message);

                    var notificationType = eventType switch
                    {
                        "payment.succeeded" => NotificationType.PAYMENT_SUCCEEDED,
                        "payment.failed" => NotificationType.PAYMENT_FAILED,
                        _ => NotificationType.PAYMENT_SUCCEEDED
                    };

                    var orderId = GetString(message, "orderId") ?? GetString(message, "order", "id") ?? paymentId;

                    await notificationService.CreateFromEventAsync(
                        notificationType,
                        userId,
                        paymentId,
                        metadata,
                        notificationType == NotificationType.PAYMENT_SUCCEEDED ? "Payment completed" : "Payment failed",
                        BuildPaymentMessage(message, orderId, paymentId, notificationType));
                }

                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process message {DeliveryTag} from {QueueName}; requeueing it.", ea.DeliveryTag, queueName);
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("RabbitMQ consumer registered for queue {QueueName}.", queueName);
    }

    private static string? GetString(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString()
        };
    }

    private static string? GetString(Dictionary<string, JsonElement>? data, string parentKey, string childKey)
    {
        var nestedData = GetObject(data, parentKey);
        return GetString(nestedData, childKey);
    }

    private static Dictionary<string, JsonElement>? UnwrapEventData(Dictionary<string, JsonElement>? envelope)
    {
        if (envelope is null || !envelope.TryGetValue("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return envelope;
        }

        return data.Deserialize<Dictionary<string, JsonElement>>();
    }

    private static string BuildMetadata(Dictionary<string, JsonElement>? data)
    {
        if (data is null || data.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(data);
    }

    private static string BuildOrderMessage(Dictionary<string, JsonElement>? data, string orderId)
    {
        var status = GetString(data, "status") ?? "unknown";
        var total = GetString(data, "total") ?? "0.00";
        return BuildOrderNotificationMessage(data, "🛒 *NEW ORDER CREATED*", orderId, status, total, "✅ Order created successfully!");
    }

    private static string BuildPaymentMessage(
        Dictionary<string, JsonElement>? data,
        string orderId,
        string paymentId,
        NotificationType notificationType)
    {
        var status = GetString(data, "status") ?? GetString(data, "payment", "status") ??
            (notificationType == NotificationType.PAYMENT_SUCCEEDED ? "succeeded" : "failed");
        var total = GetString(data, "total") ?? GetString(data, "payment", "amount") ?? "0.00";
        var confirmation = notificationType == NotificationType.PAYMENT_SUCCEEDED
            ? "✅ Payment completed successfully!"
            : "❌ Payment failed. Please check the payment details.";
        var header = notificationType == NotificationType.PAYMENT_SUCCEEDED
            ? "✅ *PAYMENT COMPLETED*"
            : "❌ *PAYMENT FAILED*";

        return BuildOrderNotificationMessage(data, header, orderId, status, total, confirmation, paymentId);
    }

    private static string BuildOrderNotificationMessage(
        Dictionary<string, JsonElement>? data,
        string header,
        string orderId,
        string status,
        string total,
        string confirmation,
        string? paymentId = null)
    {
        var payment = GetObject(data, "payment");
        var paymentMethod = GetString(data, "payment", "method") ?? GetString(data, "method") ?? "unknown";
        var paymentStatus = GetString(data, "payment", "status") ?? GetString(data, "paymentStatus") ?? status;
        var currency = GetString(data, "payment", "currency") ?? GetString(data, "currency") ?? "BRL";
        var paymentAmount = GetString(data, "payment", "amount") ?? GetString(data, "amount") ?? total;
        var amountInCents = GetString(data, "payment", "amountInCents") ?? GetString(data, "amountInCents") ?? "unknown";
        var createdAt = GetString(data, "createdAt") ?? "unknown";
        var successUrl = GetString(payment, "successUrl");
        var cancelUrl = GetString(payment, "cancelUrl");
        var paymentIdLine = string.IsNullOrWhiteSpace(paymentId) ? string.Empty : $"• Payment ID: `{paymentId}`\n";

        return header + "\n\n" +
            "📦 *Order Information*\n" +
            $"• Order ID: `{orderId}`\n" +
            $"• Order Status: `{status}`\n" +
            $"• Total: `{currency} {total}`\n\n" +
            "💳 *Payment*\n" +
            paymentIdLine +
            $"• Method: `{paymentMethod}`\n" +
            $"• Status: `{paymentStatus}`\n" +
            $"• Amount: `{currency} {paymentAmount}`\n" +
            $"• Amount in cents: `{amountInCents}`\n\n" +
            "📋 *Items*\n" +
            BuildItemLines(data) + "\n\n" +
            "🕒 *Created at*\n" +
            $"`{createdAt}`\n\n" +
            BuildCheckoutUrls(successUrl, cancelUrl) +
            confirmation;
    }

    private static string BuildItemLines(Dictionary<string, JsonElement>? data)
    {
        if (data is null || !data.TryGetValue("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return "- None";
        }

        var lines = items.EnumerateArray().Select(BuildItemLine).ToArray();

        return lines.Length == 0 ? "- None" : string.Join('\n', lines);
    }

    private static string BuildItemLine(JsonElement item)
    {
        var productId = GetString(item, "productId") ?? "unknown";
        var name = GetString(item, "name") ?? productId;
        var quantity = GetString(item, "quantity") ?? "0";
        var unitPrice = GetString(item, "unitPrice") ?? "0.00";
        var subtotal = GetString(item, "subtotal") ?? "0.00";
        return $"• *{name}*\n  - Quantity: `{quantity}`\n  - Unit price: `{unitPrice}`\n  - Subtotal: `{subtotal}`\n  - Product ID: `{productId}`";
    }

    private static int GetItemCount(Dictionary<string, JsonElement>? data)
    {
        return data is not null && data.TryGetValue("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.GetArrayLength()
            : 0;
    }

    private static string BuildCheckoutUrls(string? successUrl, string? cancelUrl)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(successUrl))
        {
            urls.Add($"Success URL: {successUrl}");
        }

        if (!string.IsNullOrWhiteSpace(cancelUrl))
        {
            urls.Add($"Cancel URL: {cancelUrl}");
        }

        return urls.Count == 0 ? string.Empty : $"🔗 *Checkout URLs*\n{string.Join('\n', urls)}\n\n";
    }

    private static Dictionary<string, JsonElement>? GetObject(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value.Deserialize<Dictionary<string, JsonElement>>();
    }

    private static string? GetString(JsonElement value, string key)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => property.ToString()
        };
    }
}
