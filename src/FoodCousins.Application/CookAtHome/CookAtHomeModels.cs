using FoodCousins.Domain.Enums;

namespace FoodCousins.Application.CookAtHome;

public sealed record CreateCookAtHomeOrderRequest(
    Guid FoodId,
    int PeopleCount,
    string Location,
    DateTimeOffset RequestedForUtc,
    string ContactName,
    string ContactPhone,
    string? SpecialInstructions);

public sealed record CookAtHomeOrderDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    Guid CookProfileId,
    string CookName,
    Guid FoodId,
    string FoodName,
    int PeopleCount,
    string Location,
    DateTimeOffset RequestedForUtc,
    string ContactName,
    string ContactPhone,
    string? SpecialInstructions,
    CookAtHomeOrderStatus Status,
    decimal? EstimatedPrice,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record UpdateCookAtHomeStatusRequest(CookAtHomeOrderStatus Status);
public sealed record CookAtHomeOrderRequestedEvent(Guid OrderId);
