using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Email;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Functions.Functions;

public sealed class OrderEventsFunction(
    FoodCousinsDbContext db,
    IEmailSender emailSender,
    ILogger<OrderEventsFunction> logger)
{
    [Function(nameof(OrderEventsFunction))]
    public async Task Run(
        [ServiceBusTrigger("notifications", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(message.Subject, "IngredientKitOrderConfirmed", StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "Ignoring unsupported notification subject. Subject={Subject}, MessageId={MessageId}",
                    message.Subject,
                    message.MessageId);
                return;
            }

            var evt = JsonSerializer.Deserialize<IngredientKitOrderConfirmedEvent>(message.Body.ToString())
                ?? throw new InvalidOperationException("IngredientKitOrderConfirmed body is invalid.");

            var order = await db.CookAtHomeOrders
                .Include(x => x.Customer)
                .Include(x => x.Food)
                .Include(x => x.Ingredients)
                .SingleOrDefaultAsync(x => x.Id == evt.OrderId, cancellationToken)
                ?? throw new KeyNotFoundException($"Cook at Home order {evt.OrderId} was not found.");

            if (order.EmailSentAtUtc is not null)
            {
                logger.LogInformation(
                    "Confirmation email already handled. OrderId={OrderId}, MessageId={MessageId}",
                    order.Id,
                    message.MessageId);
                return;
            }

            var email = BuildEmail(order);
            await emailSender.SendAsync(email, cancellationToken);

            order.EmailSentAtUtc = DateTimeOffset.UtcNow;
            order.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ingredient-kit confirmation email processed. OrderId={OrderId}, MessageId={MessageId}",
                order.Id,
                message.MessageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Notification processing was cancelled. InvocationId={InvocationId}, MessageId={MessageId}",
                context.InvocationId,
                message.MessageId);
            throw;
        }
        catch (ServiceBusException ex)
        {
            logger.LogError(
                ex,
                "Service Bus error while processing notification event. InvocationId={InvocationId}, MessageId={MessageId}, Reason={Reason}, IsTransient={IsTransient}",
                context.InvocationId,
                message.MessageId,
                ex.Reason,
                ex.IsTransient);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Notification processing failed. InvocationId={InvocationId}, MessageId={MessageId}, Subject={Subject}, DeliveryCount={DeliveryCount}",
                context.InvocationId,
                message.MessageId,
                message.Subject,
                message.DeliveryCount);
            throw;
        }
    }

    private static EmailMessage BuildEmail(FoodCousins.Domain.Entities.CookAtHomeOrder order)
    {
        var ingredientLines = order.Ingredients
            .OrderBy(x => x.IngredientName)
            .Select(x => $"- {x.IngredientName}: {x.Quantity:0.##} {x.Unit}{(x.IsOptional ? " (optional)" : string.Empty)}")
            .ToList();

        var plain = new StringBuilder()
            .AppendLine($"FoodCousins order {order.OrderNumber}")
            .AppendLine()
            .AppendLine($"Food: {order.FoodName}")
            .AppendLine($"People: {order.PeopleCount}")
            .AppendLine($"Subtotal: ${order.Subtotal:0.00}")
            .AppendLine($"Delivery: ${order.DeliveryFee:0.00}")
            .AppendLine($"Total: ${order.TotalAmount:0.00}")
            .AppendLine($"Estimated delivery: {order.EstimatedDeliveryUtc:yyyy-MM-dd HH:mm} UTC")
            .AppendLine()
            .AppendLine("Ingredients/components:")
            .AppendLine(string.Join(Environment.NewLine, ingredientLines))
            .AppendLine()
            .AppendLine(order.RecipeTitle)
            .AppendLine(order.RecipeInstructions)
            .AppendLine()
            .AppendLine("Your ingredient kit will be prepared and delivered soon.")
            .ToString();

        var htmlIngredients = string.Join(
            string.Empty,
            order.Ingredients.OrderBy(x => x.IngredientName)
                .Select(x => $"<li>{WebUtility.HtmlEncode(x.IngredientName)}: {x.Quantity:0.##} {WebUtility.HtmlEncode(x.Unit)}{(x.IsOptional ? " (optional)" : string.Empty)}</li>"));

        var html = $"""
            <h2>FoodCousins order {WebUtility.HtmlEncode(order.OrderNumber)}</h2>
            <p><strong>{WebUtility.HtmlEncode(order.FoodName)}</strong> for {order.PeopleCount} people</p>
            <p>Subtotal: ${order.Subtotal:0.00}<br/>
               Delivery: ${order.DeliveryFee:0.00}<br/>
               <strong>Total: ${order.TotalAmount:0.00}</strong></p>
            <p>Estimated delivery: {order.EstimatedDeliveryUtc:yyyy-MM-dd HH:mm} UTC</p>
            <h3>Ingredients/components</h3>
            <ul>{htmlIngredients}</ul>
            <h3>{WebUtility.HtmlEncode(order.RecipeTitle)}</h3>
            <pre style="white-space:pre-wrap;font-family:inherit">{WebUtility.HtmlEncode(order.RecipeInstructions)}</pre>
            <p>Your ingredient kit will be prepared and delivered soon.</p>
            """;

        return new EmailMessage(
            order.Customer.Email,
            order.Customer.DisplayName,
            $"FoodCousins order {order.OrderNumber} confirmed",
            plain,
            html);
    }
}
