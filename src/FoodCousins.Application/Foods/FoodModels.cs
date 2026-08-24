namespace FoodCousins.Application.Foods;

public sealed record FoodSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string Cuisine,
    string? CountryOrRegion,
    string? Tags,
    decimal KitPricePerPerson,
    bool IsPublished,
    bool IsCookAtHomeEnabled,
    string? ImageUrl,
    bool AiGenerated,
    bool AiReviewed);

public sealed record FoodIngredientDto(
    string Name,
    decimal QuantityPerServing,
    string Unit,
    bool IsOptional,
    string? Notes);

public sealed record RecipeStepDto(int StepNumber, string Instruction);

public sealed record RecipeDto(
    string Title,
    int PrepMinutes,
    int CookMinutes,
    int Servings,
    IReadOnlyList<RecipeStepDto> Steps);

public sealed record SimilarityBreakdownDto(
    decimal Overall,
    decimal Taste,
    decimal Texture,
    decimal Ingredients,
    decimal CookingMethod,
    decimal DishType,
    decimal Cuisine,
    decimal Dietary,
    string WhySimilar);

public sealed record FoodCousinDto(
    FoodSummaryDto Food,
    SimilarityBreakdownDto Similarity);

public sealed record FoodSearchResultDto(
    string Query,
    string Source,
    FoodSummaryDto Food,
    IReadOnlyList<FoodCousinDto> Cousins);

public sealed record FoodDetailDto(
    FoodSummaryDto Food,
    RecipeDto Recipe,
    IReadOnlyList<FoodIngredientDto> Ingredients,
    IReadOnlyList<FoodCousinDto> Cousins);

public sealed record AdminFoodUpdateRequest(
    bool IsPublished,
    bool AiReviewed,
    bool IsCookAtHomeEnabled,
    decimal KitPricePerPerson);
