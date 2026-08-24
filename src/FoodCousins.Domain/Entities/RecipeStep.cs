namespace FoodCousins.Domain.Entities;

public sealed class RecipeStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public int StepNumber { get; set; }
    public required string Instruction { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
