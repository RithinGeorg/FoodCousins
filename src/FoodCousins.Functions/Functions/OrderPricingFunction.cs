using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Functions.Functions;

public sealed class OrderPricingFunction(FoodCousinsDbContext db, ILogger<OrderPricingFunction> logger)
{
    [Function(nameof(OrderPricingFunction))]
    public async Task Run(
        [ServiceBusTrigger("order-processing", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "Pricing ingredient-kit order. InvocationId={InvocationId}, MessageId={MessageId}, DeliveryCount={DeliveryCount}, Subject={Subject}",
                context.InvocationId,
                message.MessageId,
                message.DeliveryCount,
                message.Subject);

            var evt = JsonSerializer.Deserialize<CookAtHomeOrderRequestedEvent>(message.Body.ToString())
                ?? throw new InvalidOperationException("CookAtHomeOrderRequested body is invalid.");

            var order = await db.CookAtHomeOrders
                .Include(x => x.Food)
                .Include(x => x.StatusHistory)
                .SingleOrDefaultAsync(x => x.Id == evt.OrderId, cancellationToken)
                ?? throw new KeyNotFoundException($"Cook at Home order {evt.OrderId} was not found.");

            if (order.Status is CookAtHomeOrderStatus.Confirmed
                or CookAtHomeOrderStatus.Preparing
                or CookAtHomeOrderStatus.ReadyForDelivery
                or CookAtHomeOrderStatus.OutForDelivery
                or CookAtHomeOrderStatus.Delivered
                or CookAtHomeOrderStatus.Cancelled)
            {
                logger.LogInformation(
                    "Pricing message already handled. OrderId={OrderId}, Status={Status}, MessageId={MessageId}",
                    order.Id,
                    order.Status,
                    message.MessageId);
                return;
            }

            if (order.Status == CookAtHomeOrderStatus.Requested)
            {
                order.TransitionTo(CookAtHomeOrderStatus.Processing, "Pricing started by OrderPricingFunction.");
                await db.SaveChangesAsync(cancellationToken);
            }

            if (order.Status != CookAtHomeOrderStatus.Processing)
                throw new InvalidOperationException($"Order {order.Id} is in unexpected status {order.Status} for pricing.");

            var rawSubtotal = decimal.Round(
                                order.KitPricePerPerson * order.PeopleCount,
                                2,
                                MidpointRounding.AwayFromZero);

            // The configured minimum order is a commercial floor for the kit subtotal.
            order.Subtotal = Math.Max(
                                    rawSubtotal,
                                    order.MinimumOrderAmount);
            order.TotalAmount = order.Subtotal + order.DeliveryFee;

            var earliest =
                            order.CreatedAtUtc.AddMinutes(
                             order.DeliveryEstimateMinutes);

            order.TransitionTo(CookAtHomeOrderStatus.Confirmed, "Price and delivery estimate calculated.");

            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "IngredientKitOrderConfirmed",
                Payload = JsonSerializer.Serialize(new IngredientKitOrderConfirmedEvent(order.Id))
            });

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ingredient-kit order priced. OrderId={OrderId}, Subtotal={Subtotal}, DeliveryFee={DeliveryFee}, Total={Total}, EstimatedDeliveryUtc={EstimatedDeliveryUtc}",
                order.Id,
                order.Subtotal,
                order.DeliveryFee,
                order.TotalAmount,
                order.EstimatedDeliveryUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Order pricing was cancelled. InvocationId={InvocationId}, MessageId={MessageId}",
                context.InvocationId,
                message.MessageId);
            throw;
        }
        catch (ServiceBusException ex)
        {
            logger.LogError(
                ex,
                "Service Bus error during order pricing. InvocationId={InvocationId}, MessageId={MessageId}, Reason={Reason}, IsTransient={IsTransient}",
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
                "Order pricing failed. InvocationId={InvocationId}, MessageId={MessageId}, DeliveryCount={DeliveryCount}",
                context.InvocationId,
                message.MessageId,
                message.DeliveryCount);
            throw;
        }
    }
}
