namespace FoodCousins.Domain.Entities;
public sealed class Food
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CookProfileId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Cuisine { get; set; }
    public string? Tags { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? ImageUrl { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public CookProfile CookProfile { get; set; } = null!;
}
