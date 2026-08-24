namespace FoodCousins.Domain.Entities;

public sealed class CookAtHomeOrderIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CookAtHomeOrderId { get; set; }
    public required string IngredientName { get; set; }
    public decimal Quantity { get; set; }
    public required string Unit { get; set; }
    public bool IsOptional { get; set; }
    public string? Notes { get; set; }

    public CookAtHomeOrder CookAtHomeOrder { get; set; } = null!;
}
