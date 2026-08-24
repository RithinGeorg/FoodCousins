namespace FoodCousins.Domain.Entities;

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }

    public List<FoodIngredient> Foods { get; set; } = [];
}
