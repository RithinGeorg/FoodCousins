using FoodCousins.Domain.Enums;

namespace FoodCousins.Domain.Entities;

public sealed class CookAtHomeOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CookProfileId { get; set; }
    public Guid FoodId { get; set; }
    public required string IdempotencyKey { get; set; }
    public int PeopleCount { get; set; }
    public required string Location { get; set; }
    public DateTimeOffset RequestedForUtc { get; set; }
    public required string ContactName { get; set; }
    public required string ContactPhone { get; set; }
    public string? SpecialInstructions { get; set; }
    public CookAtHomeOrderStatus Status { get; set; } = CookAtHomeOrderStatus.Requested;
    public decimal? EstimatedPrice { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PricedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User Customer { get; set; } = null!;
    public CookProfile CookProfile { get; set; } = null!;
    public Food Food { get; set; } = null!;
    public List<CookAtHomeOrderStatusHistory> StatusHistory { get; set; } = [];

    public bool CanTransitionTo(CookAtHomeOrderStatus next) => (Status, next) switch
    {
        (CookAtHomeOrderStatus.Requested, CookAtHomeOrderStatus.Processing) => true,
        (CookAtHomeOrderStatus.Requested, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.Processing, CookAtHomeOrderStatus.PriceCalculated) => true,
        (CookAtHomeOrderStatus.Processing, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.PriceCalculated, CookAtHomeOrderStatus.Confirmed) => true,
        (CookAtHomeOrderStatus.PriceCalculated, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.Confirmed, CookAtHomeOrderStatus.AcceptedByCook) => true,
        (CookAtHomeOrderStatus.Confirmed, CookAtHomeOrderStatus.Rejected) => true,
        (CookAtHomeOrderStatus.Confirmed, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.AcceptedByCook, CookAtHomeOrderStatus.Preparing) => true,
        (CookAtHomeOrderStatus.AcceptedByCook, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.Preparing, CookAtHomeOrderStatus.Completed) => true,
        _ => false
    };

    public void TransitionTo(CookAtHomeOrderStatus next)
    {
        if (!CanTransitionTo(next))
            throw new InvalidOperationException($"Cannot transition cook-at-home order from {Status} to {next}.");

        Status = next;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (next == CookAtHomeOrderStatus.PriceCalculated) PricedAtUtc = UpdatedAtUtc;
    }
}
