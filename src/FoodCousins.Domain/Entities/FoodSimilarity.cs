namespace FoodCousins.Domain.Entities;

public sealed class FoodSimilarity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceFoodId { get; set; }
    public Guid RelatedFoodId { get; set; }

    public decimal OverallScore { get; set; }
    public decimal TasteScore { get; set; }
    public decimal TextureScore { get; set; }
    public decimal IngredientScore { get; set; }
    public decimal CookingMethodScore { get; set; }
    public decimal DishTypeScore { get; set; }
    public decimal CuisineScore { get; set; }
    public decimal DietaryScore { get; set; }
    public required string WhySimilar { get; set; }

    public Food SourceFood { get; set; } = null!;
    public Food RelatedFood { get; set; } = null!;
}
