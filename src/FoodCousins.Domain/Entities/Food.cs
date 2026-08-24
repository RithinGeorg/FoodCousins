namespace FoodCousins.Domain.Entities;

public sealed class Food
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public required string Cuisine { get; set; }
    public string? CountryOrRegion { get; set; }
    public string? Tags { get; set; }
    public string? ImageUrl { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool AiGenerated { get; set; }
    public bool AiReviewed { get; set; }

    public bool IsCookAtHomeEnabled { get; set; } = true;
    public decimal KitPricePerPerson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Recipe? Recipe { get; set; }
    public List<FoodIngredient> Ingredients { get; set; } = [];
    public List<FoodSimilarity> Similarities { get; set; } = [];
}
