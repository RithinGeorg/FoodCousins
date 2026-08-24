using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;

namespace FoodCousins.UnitTests;

public sealed class OrderTests
{
    [Fact]
    public void IngredientKitOrder_FollowsDeliveryLifecycle()
    {
        var order = CreateOrder();

        order.TransitionTo(CookAtHomeOrderStatus.Processing);
        order.TransitionTo(CookAtHomeOrderStatus.Confirmed);
        order.TransitionTo(CookAtHomeOrderStatus.Preparing);
        order.TransitionTo(CookAtHomeOrderStatus.ReadyForDelivery);
        order.TransitionTo(CookAtHomeOrderStatus.OutForDelivery);
        order.TransitionTo(CookAtHomeOrderStatus.Delivered);

        Assert.Equal(CookAtHomeOrderStatus.Delivered, order.Status);
        Assert.Equal(6, order.StatusHistory.Count);
    }

    [Fact]
    public void IngredientKitOrder_RejectsInvalidTransition()
    {
        var order = CreateOrder();

        Assert.Throws<InvalidOperationException>(() =>
            order.TransitionTo(CookAtHomeOrderStatus.Delivered));
    }

    private static CookAtHomeOrder CreateOrder() => new()
    {
        OrderNumber = "FCK-TEST-001",
        CustomerId = Guid.NewGuid(),
        FoodId = Guid.NewGuid(),
        FoodName = "Biryani",
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        PeopleCount = 2,
        Postcode = "4227",
        Suburb = "Varsity Lakes",
        DeliveryAddress = "Test address",
        RequestedDeliveryUtc = DateTimeOffset.UtcNow.AddHours(4),
        ContactName = "Test Customer",
        ContactPhone = "0400000000",
        RecipeTitle = "Test recipe",
        RecipeInstructions = "1. Cook it."
    };
}
