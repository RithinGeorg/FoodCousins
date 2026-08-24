namespace FoodCousins.Application.Foods;

public interface IFoodService
{
    Task<IReadOnlyList<FoodSummaryDto>> GetPublishedAsync(CancellationToken cancellationToken);
    Task<FoodSearchResultDto> SearchAsync(string query, CancellationToken cancellationToken);
    Task<FoodDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodCousinDto>> GetCousinsAsync(Guid id, CancellationToken cancellationToken);
}
