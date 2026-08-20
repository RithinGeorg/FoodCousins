using FoodCousins.Domain.Enums;
namespace FoodCousins.Application.Orders;
public interface IOrderService
{
    Task<OrderDto> PlaceAsync(Guid customerId, string idempotencyKey, PlaceOrderRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(Guid customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderDto>> GetCookOrdersAsync(Guid cookUserId, CancellationToken cancellationToken);
    Task<OrderDto> UpdateCookOrderStatusAsync(Guid cookUserId, Guid orderId, OrderStatus status, CancellationToken cancellationToken);
}
