using FoodCousins.Domain.Enums;
namespace FoodCousins.Domain.Entities;
public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CookProfileId { get; set; }
    public required string IdempotencyKey { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public string FulfilmentType { get; set; } = "Pickup";
    public string? DeliveryAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public User Customer { get; set; } = null!;
    public CookProfile CookProfile { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = [];

    public bool CanTransitionTo(OrderStatus next) => (Status, next) switch
    {
        (OrderStatus.Pending, OrderStatus.Confirmed) => true,
        (OrderStatus.Pending, OrderStatus.Cancelled) => true,
        (OrderStatus.Confirmed, OrderStatus.AcceptedByCook) => true,
        (OrderStatus.Confirmed, OrderStatus.Rejected) => true,
        (OrderStatus.AcceptedByCook, OrderStatus.Preparing) => true,
        (OrderStatus.AcceptedByCook, OrderStatus.Cancelled) => true,
        (OrderStatus.Preparing, OrderStatus.Ready) => true,
        (OrderStatus.Ready, OrderStatus.Completed) => true,
        _ => false
    };

    public void TransitionTo(OrderStatus next)
    {
        if (!CanTransitionTo(next))
            throw new InvalidOperationException($"Cannot transition order from {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
