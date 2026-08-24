using FoodCousins.Domain.Enums;

namespace FoodCousins.Application.CookAtHome;

public sealed record CreateCookAtHomeOrderRequest(
    Guid FoodId,
    int PeopleCount,
    string Postcode,
    string DeliveryAddress,
    DateTimeOffset RequestedDeliveryUtc,
    string ContactName,
    string ContactPhone,
    string? SpecialInstructions);

public sealed record CookAtHomeOrderIngredientDto(
    string IngredientName,
    decimal Quantity,
    string Unit,
    bool IsOptional,
    string? Notes);

public sealed record CookAtHomeOrderStatusHistoryDto(
    CookAtHomeOrderStatus Status,
    string? Note,
    DateTimeOffset CreatedAtUtc);

public sealed record CookAtHomeOrderDto(
    Guid Id,
    string OrderNumber,
    Guid FoodId,
    string FoodName,
    int PeopleCount,
    string Postcode,
    string Suburb,
    string DeliveryAddress,
    DateTimeOffset RequestedDeliveryUtc,
    DateTimeOffset? EstimatedDeliveryUtc,
    string ContactName,
    string ContactPhone,
    string? SpecialInstructions,
    CookAtHomeOrderStatus Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal TotalAmount,
    string RecipeTitle,
    string RecipeInstructions,
    DateTimeOffset? EmailSentAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CookAtHomeOrderIngredientDto> Ingredients,
    IReadOnlyList<CookAtHomeOrderStatusHistoryDto> StatusHistory);

public sealed record CookAtHomeOrderRequestedEvent(Guid OrderId);
public sealed record IngredientKitOrderConfirmedEvent(Guid OrderId);
