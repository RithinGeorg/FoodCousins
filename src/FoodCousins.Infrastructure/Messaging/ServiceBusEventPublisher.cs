using Azure.Identity;
using Azure.Messaging.ServiceBus;
using FoodCousins.Application.Messaging;
using Microsoft.Extensions.Configuration;

namespace FoodCousins.Infrastructure.Messaging;

public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _orderProcessingSender;
    private readonly ServiceBusSender _notificationsSender;

    public ServiceBusEventPublisher(IConfiguration configuration)
    {
        var orderProcessingQueue = configuration["ServiceBus:OrderProcessingQueue"] ?? "order-processing";
        var notificationsQueue = configuration["ServiceBus:NotificationsQueue"] ?? "notifications";
        var connectionString = configuration["ServiceBus:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _client = new ServiceBusClient(connectionString);
        }
        else
        {
            var fqns = configuration["ServiceBus:FullyQualifiedNamespace"]
                ?? throw new InvalidOperationException("ServiceBus:FullyQualifiedNamespace is required.");
            _client = new ServiceBusClient(fqns, new DefaultAzureCredential());
        }

        _orderProcessingSender = _client.CreateSender(orderProcessingQueue);
        _notificationsSender = _client.CreateSender(notificationsQueue);
    }

    public Task PublishAsync(string type, string payload, CancellationToken ct)
    {
        var sender = string.Equals(type, "CookAtHomeOrderRequested", StringComparison.Ordinal)
            ? _orderProcessingSender
            : _notificationsSender;

        var message = new ServiceBusMessage(payload)
        {
            Subject = type,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N")
        };

        return sender.SendMessageAsync(message, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _orderProcessingSender.DisposeAsync();
        await _notificationsSender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
