using System.Text;
using System.Text.Json;
using MyApi.Models;

namespace MyApi.Services;

public class SlackNotificationProvider : INotificationProvider
{
    private readonly HttpClient httpClient;
    private readonly string webhookUrl;

    public SlackNotificationProvider(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        webhookUrl = configuration["SLACK_WEBHOOK_URL"] ?? "";
    }

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        var payload = new
        {
            text = $"[{notification.Type}] {notification.Title}\n{notification.Message}"
        };

        var response = await httpClient.PostAsync(
            webhookUrl,
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
