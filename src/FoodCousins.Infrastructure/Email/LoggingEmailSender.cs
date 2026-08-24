using FoodCousins.Application.Email;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Infrastructure.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "DEV EMAIL. To={ToEmail}, Subject={Subject}, Body={Body}",
            message.ToEmail,
            message.Subject,
            message.PlainTextBody);

        return Task.CompletedTask;
    }
}
