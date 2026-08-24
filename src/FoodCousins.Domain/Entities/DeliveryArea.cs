namespace FoodCousins.Domain.Entities;

public sealed class DeliveryArea
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Postcode { get; set; }
    public required string Suburb { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal DeliveryFee { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public int EstimatedDeliveryMinutes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
