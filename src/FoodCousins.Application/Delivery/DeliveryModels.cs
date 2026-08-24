namespace FoodCousins.Application.Delivery;

public sealed record DeliveryAreaDto(
    Guid Id,
    string Postcode,
    string Suburb,
    bool IsActive,
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    int EstimatedDeliveryMinutes);

public sealed record DeliveryAvailabilityDto(
    string Postcode,
    string? Suburb,
    bool Available,
    decimal? DeliveryFee,
    decimal? MinimumOrderAmount,
    int? EstimatedDeliveryMinutes);

public sealed record UpsertDeliveryAreaRequest(
    string Postcode,
    string Suburb,
    bool IsActive,
    decimal DeliveryFee,
    decimal MinimumOrderAmount,
    int EstimatedDeliveryMinutes);
