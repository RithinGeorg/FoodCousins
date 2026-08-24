namespace FoodCousins.Application.Foods;

public interface IFoodDiscoveryProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<AiFoodDiscoveryContract> DiscoverAsync(string query, CancellationToken cancellationToken);
}
