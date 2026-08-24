namespace FoodCousins.Application.Delivery;

public interface IDeliveryAreaService
{
    Task<DeliveryAvailabilityDto> CheckAsync(string postcode, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryAreaDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<DeliveryAreaDto> UpsertAsync(UpsertDeliveryAreaRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken);
}
