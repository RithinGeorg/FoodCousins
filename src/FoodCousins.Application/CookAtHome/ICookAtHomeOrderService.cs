using FoodCousins.Domain.Enums;

namespace FoodCousins.Application.CookAtHome;

public interface ICookAtHomeOrderService
{
    Task<CookAtHomeOrderDto> RequestAsync(Guid customerId, string idempotencyKey, CreateCookAtHomeOrderRequest request, CancellationToken ct);
    Task<IReadOnlyList<CookAtHomeOrderDto>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<CookAtHomeOrderDto>> GetCookOrdersAsync(Guid cookUserId, CancellationToken ct);
    Task<CookAtHomeOrderDto> ConfirmQuoteAsync(Guid customerId, Guid orderId, CancellationToken ct);
    Task<CookAtHomeOrderDto> CancelAsync(Guid customerId, Guid orderId, CancellationToken ct);
    Task<CookAtHomeOrderDto> UpdateCookStatusAsync(Guid cookUserId, Guid orderId, CookAtHomeOrderStatus status, CancellationToken ct);
}
