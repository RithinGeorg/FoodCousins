namespace FoodCousins.Application.Foods;
public interface IFoodService
{
    Task<IReadOnlyList<FoodDto>> GetAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodDto>> SearchAsync(string? query, string? cuisine, CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodCousinDto>> GetCousinsAsync(Guid id, CancellationToken cancellationToken);
    Task<FoodDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodDto>> GetCookFoodsAsync(Guid userId, CancellationToken cancellationToken);
    Task<FoodDto> CreateAsync(Guid userId, UpsertFoodRequest request, CancellationToken cancellationToken);
    Task<FoodDto> UpdateAsync(Guid userId, Guid foodId, UpsertFoodRequest request, CancellationToken cancellationToken);
    Task<FoodDto> SetImageAsync(Guid userId, Guid foodId, Stream content, string contentType, string fileName, CancellationToken cancellationToken);
}
