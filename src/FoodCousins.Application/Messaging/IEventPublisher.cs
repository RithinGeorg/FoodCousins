namespace FoodCousins.Application.Messaging;
public interface IEventPublisher
{
    Task PublishAsync(string type, string payload, CancellationToken cancellationToken);
}
