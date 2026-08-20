namespace FoodCousins.Application.Foods;
public sealed record FoodDto(Guid Id, Guid CookProfileId, string CookName, string Name, string? Description, string Cuisine, string? Tags, decimal Price, bool IsAvailable, string? ImageUrl);
public sealed record FoodCousinDto(FoodDto Food, double Score, string Why);
public sealed record UpsertFoodRequest(string Name, string? Description, string Cuisine, decimal Price, bool IsAvailable = true, string? Tags = null);
