namespace FoodCousins.Domain.Entities;

public sealed class FoodIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FoodId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal QuantityPerServing { get; set; }
    public required string Unit { get; set; }
    public bool IsOptional { get; set; }
    public string? Notes { get; set; }

    public Food Food { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
