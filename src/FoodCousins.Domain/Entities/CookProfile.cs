namespace FoodCousins.Domain.Entities;
public sealed class CookProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string BusinessName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public User User { get; set; } = null!;
    public List<Food> Foods { get; set; } = [];
}
