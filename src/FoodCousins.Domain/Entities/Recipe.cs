namespace FoodCousins.Domain.Entities;

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FoodId { get; set; }
    public required string Title { get; set; }
    public int PrepMinutes { get; set; }
    public int CookMinutes { get; set; }
    public int Servings { get; set; }

    public Food Food { get; set; } = null!;
    public List<RecipeStep> Steps { get; set; } = [];
}
