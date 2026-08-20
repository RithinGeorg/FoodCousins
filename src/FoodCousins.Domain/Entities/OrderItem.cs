namespace FoodCousins.Domain.Entities;
public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid FoodId { get; set; }
    public required string FoodName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public Order Order { get; set; } = null!;
}
