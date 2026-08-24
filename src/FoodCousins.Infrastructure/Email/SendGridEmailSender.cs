using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FoodCousins.Application.Email;
using Microsoft.Extensions.Options;

namespace FoodCousins.Infrastructure.Email;

public sealed class SendGridEmailSender(HttpClient httpClient, IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Email:ApiKey is required when Email:Provider is SendGrid.");

        var payload = new
        {
            personalizations = new[]
            {
                new { to = new[] { new { email = message.ToEmail, name = message.ToName } } }
            },
            from = new { email = _options.FromEmail, name = _options.FromName },
            subject = message.Subject,
            content = new object[]
            {
                new { type = "text/plain", value = message.PlainTextBody },
                new { type = "text/html", value = message.HtmlBody }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"SendGrid returned HTTP {(int)response.StatusCode}: {responseBody[..Math.Min(responseBody.Length, 1000)]}");
        }
    }
}
