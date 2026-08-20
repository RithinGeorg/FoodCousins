using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Functions.Functions;

public sealed class OrderEventsFunction(ILogger<OrderEventsFunction> logger)
{
    [Function(nameof(OrderEventsFunction))]
    public Task Run(
        [ServiceBusTrigger("notifications", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "Processing notification event. InvocationId={InvocationId}, Subject={Subject}, MessageId={MessageId}, DeliveryCount={DeliveryCount}, BodySize={BodySize}",
                context.InvocationId,
                message.Subject,
                message.MessageId,
                message.DeliveryCount,
                message.Body.ToMemory().Length);

            // Dev MVP: Application Insights proves the notification event was delivered.
            // Add the selected email/SMS/in-app provider here later. Never log the payload
            // because it can contain customer order/contact information.

            logger.LogInformation(
                "Notification event processed successfully. InvocationId={InvocationId}, MessageId={MessageId}, Subject={Subject}",
                context.InvocationId,
                message.MessageId,
                message.Subject);

            return Task.CompletedTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Notification processing was cancelled. InvocationId={InvocationId}, MessageId={MessageId}, Subject={Subject}",
                context.InvocationId,
                message.MessageId,
                message.Subject);
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
                "Unhandled notification error. InvocationId={InvocationId}, MessageId={MessageId}, Subject={Subject}, DeliveryCount={DeliveryCount}",
                context.InvocationId,
                message.MessageId,
                message.Subject,
                message.DeliveryCount);
            throw;
        }
    }
}
