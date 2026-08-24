namespace FoodCousins.Application.CookAtHome;

public interface ICookAtHomeOrderService
{
    Task<CookAtHomeOrderDto> RequestAsync(
        Guid customerId,
        string idempotencyKey,
        CreateCookAtHomeOrderRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CookAtHomeOrderDto>> GetCustomerOrdersAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<CookAtHomeOrderDto?> GetCustomerOrderAsync(
        Guid customerId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<CookAtHomeOrderDto> CancelAsync(
        Guid customerId,
        Guid orderId,
        CancellationToken cancellationToken);
}
