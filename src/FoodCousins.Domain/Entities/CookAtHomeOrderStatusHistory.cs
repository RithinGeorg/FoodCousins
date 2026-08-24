using FoodCousins.Domain.Enums;

namespace FoodCousins.Domain.Entities;

public sealed class CookAtHomeOrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CookAtHomeOrderId { get; set; }
    public CookAtHomeOrderStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public CookAtHomeOrder CookAtHomeOrder { get; set; } = null!;
}
