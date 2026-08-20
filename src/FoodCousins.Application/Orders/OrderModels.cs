using FoodCousins.Domain.Enums;
namespace FoodCousins.Application.Orders;
public sealed record PlaceOrderItem(Guid FoodId, int Quantity);
public sealed record PlaceOrderRequest(IReadOnlyList<PlaceOrderItem> Items, string FulfilmentType = "Pickup", string? DeliveryAddress = null);
public sealed record OrderItemDto(Guid FoodId, string FoodName, decimal UnitPrice, int Quantity);
public sealed record OrderDto(Guid Id, string OrderNumber, Guid CustomerId, Guid CookProfileId, string CookName, OrderStatus Status, decimal TotalAmount, string FulfilmentType, string? DeliveryAddress, DateTimeOffset CreatedAtUtc, IReadOnlyList<OrderItemDto> Items);
public sealed record UpdateOrderStatusRequest(OrderStatus Status);
