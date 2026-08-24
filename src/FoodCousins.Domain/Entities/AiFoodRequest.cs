namespace FoodCousins.Domain.Entities;

public sealed class AiFoodRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Query { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public bool Succeeded { get; set; }
    public Guid? FoodId { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
