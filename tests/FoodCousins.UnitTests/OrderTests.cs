using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;

namespace FoodCousins.UnitTests;

public sealed class OrderTests
{
    [Fact]
    public void Valid_order_flow_moves_to_completed()
    {
        var order = NewOrder();
        order.TransitionTo(OrderStatus.Confirmed);
        order.TransitionTo(OrderStatus.AcceptedByCook);
        order.TransitionTo(OrderStatus.Preparing);
        order.TransitionTo(OrderStatus.Ready);
        order.TransitionTo(OrderStatus.Completed);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Invalid_transition_is_rejected()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Ready));
    }

    private static Order NewOrder() => new() { OrderNumber = "FC-TEST", CustomerId = Guid.NewGuid(), CookProfileId = Guid.NewGuid(), IdempotencyKey = "test" };
}

public sealed class CookAtHomeOrderTests
{
    [Fact]
    public void Valid_cook_at_home_flow_moves_to_completed()
    {
        var order = NewOrder();
        order.TransitionTo(CookAtHomeOrderStatus.Processing);
        order.TransitionTo(CookAtHomeOrderStatus.PriceCalculated);
        order.TransitionTo(CookAtHomeOrderStatus.Confirmed);
        order.TransitionTo(CookAtHomeOrderStatus.AcceptedByCook);
        order.TransitionTo(CookAtHomeOrderStatus.Preparing);
        order.TransitionTo(CookAtHomeOrderStatus.Completed);
        Assert.Equal(CookAtHomeOrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Cook_at_home_cannot_skip_pricing()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(CookAtHomeOrderStatus.Confirmed));
    }

    private static CookAtHomeOrder NewOrder() => new()
    {
        OrderNumber = "FCH-TEST",
        CustomerId = Guid.NewGuid(),
        CookProfileId = Guid.NewGuid(),
        FoodId = Guid.NewGuid(),
        IdempotencyKey = "test",
        PeopleCount = 2,
        Location = "Test location",
        RequestedForUtc = DateTimeOffset.UtcNow.AddHours(2),
        ContactName = "Test",
        ContactPhone = "0400000000"
    };
}
