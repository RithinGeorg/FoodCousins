namespace FoodCousins.Application.Foods;

public sealed record AiFoodDiscoveryContract(
    AiFoodContract Food,
    IReadOnlyList<AiRelatedFoodContract> Cousins);

public sealed record AiFoodContract(
    string Name,
    string Description,
    string Cuisine,
    string? CountryOrRegion,
    string? Tags,
    AiRecipeContract Recipe,
    IReadOnlyList<AiIngredientContract> Ingredients);

public sealed record AiRecipeContract(
    string Title,
    int PrepMinutes,
    int CookMinutes,
    int Servings,
    IReadOnlyList<string> Steps);

public sealed record AiIngredientContract(
    string Name,
    decimal QuantityPerServing,
    string Unit,
    bool IsOptional,
    string? Notes);

public sealed record AiRelatedFoodContract(
    AiFoodContract Food,
    AiSimilarityContract Similarity);

public sealed record AiSimilarityContract(
    decimal Overall,
    decimal Taste,
    decimal Texture,
    decimal Ingredients,
    decimal CookingMethod,
    decimal DishType,
    decimal Cuisine,
    decimal Dietary,
    string WhySimilar);
