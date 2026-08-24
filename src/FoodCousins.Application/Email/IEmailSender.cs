namespace FoodCousins.Application.Email;

public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string PlainTextBody,
    string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
