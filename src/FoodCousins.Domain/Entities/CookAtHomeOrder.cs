using FoodCousins.Domain.Enums;

namespace FoodCousins.Domain.Entities;

public sealed class CookAtHomeOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Guid FoodId { get; set; }

    public required string FoodName
    {
        get; set;
    }
    public required string IdempotencyKey { get; set; }

    public int PeopleCount { get; set; }
    public required string Postcode { get; set; }
    public required string Suburb { get; set; }
    public required string DeliveryAddress { get; set; }
    public DateTimeOffset RequestedDeliveryUtc { get; set; }
    public DateTimeOffset? EstimatedDeliveryUtc { get; set; }

    public required string ContactName { get; set; }
    public required string ContactPhone { get; set; }
    public string? SpecialInstructions { get; set; }

    public decimal Subtotal { get; set; }

    public decimal KitPricePerPerson
    {
        get; set;
    }
    public decimal MinimumOrderAmount
    {
        get; set;
    }
    public int DeliveryEstimateMinutes
    {
        get; set;
    }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    public required string RecipeTitle { get; set; }
    public required string RecipeInstructions { get; set; }

    public CookAtHomeOrderStatus Status { get; set; } = CookAtHomeOrderStatus.Requested;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PricedAtUtc { get; set; }
    public DateTimeOffset? EmailSentAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User Customer { get; set; } = null!;
    public Food Food { get; set; } = null!;
    public List<CookAtHomeOrderIngredient> Ingredients { get; set; } = [];
    public List<CookAtHomeOrderStatusHistory> StatusHistory { get; set; } = [];

    public bool CanTransitionTo(CookAtHomeOrderStatus next) => (Status, next) switch
    {
        (CookAtHomeOrderStatus.Requested, CookAtHomeOrderStatus.Processing) => true,
        (CookAtHomeOrderStatus.Requested, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.Requested, CookAtHomeOrderStatus.Failed) => true,

        (CookAtHomeOrderStatus.Processing, CookAtHomeOrderStatus.Confirmed) => true,
        (CookAtHomeOrderStatus.Processing, CookAtHomeOrderStatus.Cancelled) => true,
        (CookAtHomeOrderStatus.Processing, CookAtHomeOrderStatus.Failed) => true,

        (CookAtHomeOrderStatus.Confirmed, CookAtHomeOrderStatus.Preparing) => true,
        (CookAtHomeOrderStatus.Confirmed, CookAtHomeOrderStatus.Cancelled) => true,

        (CookAtHomeOrderStatus.Preparing, CookAtHomeOrderStatus.ReadyForDelivery) => true,
        (CookAtHomeOrderStatus.Preparing, CookAtHomeOrderStatus.Cancelled) => true,

        (CookAtHomeOrderStatus.ReadyForDelivery, CookAtHomeOrderStatus.OutForDelivery) => true,
        (CookAtHomeOrderStatus.ReadyForDelivery, CookAtHomeOrderStatus.Cancelled) => true,

        (CookAtHomeOrderStatus.OutForDelivery, CookAtHomeOrderStatus.Delivered) => true,
        _ => false
    };

    public void TransitionTo(CookAtHomeOrderStatus next, string? note = null)
    {
        if (!CanTransitionTo(next))
            throw new InvalidOperationException($"Cannot transition Cook at Home order from {Status} to {next}.");

        Status = next;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (next == CookAtHomeOrderStatus.Confirmed) PricedAtUtc = UpdatedAtUtc;

        StatusHistory.Add(new CookAtHomeOrderStatusHistory
        {
            Status = next,
            Note = note
        });
    }
}
