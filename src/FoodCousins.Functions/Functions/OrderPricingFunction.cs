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
                "Pricing cook-at-home order. InvocationId={InvocationId}, MessageId={MessageId}, DeliveryCount={DeliveryCount}, Subject={Subject}",
                context.InvocationId,
                message.MessageId,
                message.DeliveryCount,
                message.Subject);

            var evt = JsonSerializer.Deserialize<CookAtHomeOrderRequestedEvent>(message.Body.ToString())
                ?? throw new InvalidOperationException("OrderRequested message body is invalid.");

            var order = await db.CookAtHomeOrders
                .Include(x => x.Food)
                .SingleOrDefaultAsync(x => x.Id == evt.OrderId, cancellationToken)
                ?? throw new KeyNotFoundException($"Cook-at-home order {evt.OrderId} was not found.");

            if (order.Status is CookAtHomeOrderStatus.PriceCalculated
                or CookAtHomeOrderStatus.Confirmed
                or CookAtHomeOrderStatus.AcceptedByCook
                or CookAtHomeOrderStatus.Preparing
                or CookAtHomeOrderStatus.Completed
                or CookAtHomeOrderStatus.Cancelled
                or CookAtHomeOrderStatus.Rejected)
            {
                logger.LogInformation(
                    "Pricing message is already handled. OrderId={OrderId}, Status={Status}, MessageId={MessageId}",
                    order.Id,
                    order.Status,
                    message.MessageId);
                return;
            }

            if (order.Status == CookAtHomeOrderStatus.Requested)
            {
                order.TransitionTo(CookAtHomeOrderStatus.Processing);
                db.CookAtHomeOrderStatusHistory.Add(new CookAtHomeOrderStatusHistory
                {
                    CookAtHomeOrderId = order.Id,
                    Status = CookAtHomeOrderStatus.Processing,
                    Note = "Pricing started by OrderPricingFunction."
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            if (order.Status != CookAtHomeOrderStatus.Processing)
                throw new InvalidOperationException($"Order {order.Id} is in unexpected status {order.Status} for pricing.");

            // Dev pricing rule: Food.Price is the base per-person cook-at-home price.
            // Replace this with ingredient/travel/service pricing rules when the customer finalizes them.
            order.EstimatedPrice = decimal.Round(order.Food.Price * order.PeopleCount, 2, MidpointRounding.AwayFromZero);
            order.TransitionTo(CookAtHomeOrderStatus.PriceCalculated);

            db.CookAtHomeOrderStatusHistory.Add(new CookAtHomeOrderStatusHistory
            {
                CookAtHomeOrderId = order.Id,
                Status = CookAtHomeOrderStatus.PriceCalculated,
                Note = "Quote calculated by OrderPricingFunction."
            });

            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "CookAtHomeQuoteReady",
                Payload = JsonSerializer.Serialize(new
                {
                    order.Id,
                    order.OrderNumber,
                    order.CustomerId,
                    order.CookProfileId,
                    order.FoodId,
                    order.PeopleCount,
                    order.EstimatedPrice,
                    Status = order.Status.ToString(),
                    order.PricedAtUtc
                })
            });

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Cook-at-home quote calculated. OrderId={OrderId}, EstimatedPrice={EstimatedPrice}, MessageId={MessageId}",
                order.Id,
                order.EstimatedPrice,
                message.MessageId);
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
