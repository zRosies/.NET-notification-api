using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyApi.Models;

namespace MyApi.Services;

public class BrevoNotificationProvider : INotificationProvider
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public BrevoNotificationProvider(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        apiKey = configuration["BREVO_API_KEY"] ?? "";
        httpClient.BaseAddress = new Uri("https://api.brevo.com/");
    }

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var payload = new
        {
            sender = new { name = "MyApi", email = "no-reply@example.com" },
            to = new[]
            {
                new { email = notification.RecipientId }
            },
            subject = notification.Title,
            htmlContent = $"<html><body><p>{notification.Message}</p></body></html>"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("api-key", apiKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
