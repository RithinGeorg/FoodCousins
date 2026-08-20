using System.Text.Json;
using FoodCousins.Application.Orders;
using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Orders;

public sealed class OrderService(FoodCousinsDbContext db) : IOrderService
{
    public async Task<OrderDto> PlaceAsync(Guid customerId, string idempotencyKey, PlaceOrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency-Key is required.");
        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length > 120) throw new ArgumentException("Idempotency-Key is too long.");
        if (request.Items is null || request.Items.Count == 0) throw new ArgumentException("At least one item is required.");
        if (request.Items.Count > 50) throw new ArgumentException("An order can contain at most 50 line items.");
        if (request.Items.Any(x => x.Quantity <= 0 || x.Quantity > 100)) throw new ArgumentException("Each quantity must be between 1 and 100.");
        if (!string.IsNullOrWhiteSpace(request.DeliveryAddress) && request.DeliveryAddress.Trim().Length > 1000) throw new ArgumentException("Delivery address is too long.");

        var existing = await QueryOrders().FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) return Map(existing);

        var requestedIds = request.Items.Select(x => x.FoodId).Distinct().ToArray();
        var foods = await db.Foods.Include(x => x.CookProfile)
            .Where(x => requestedIds.Contains(x.Id) && x.IsAvailable && x.CookProfile.IsActive)
            .ToListAsync(ct);

        if (foods.Count != requestedIds.Length) throw new InvalidOperationException("One or more foods are unavailable.");
        var cookIds = foods.Select(x => x.CookProfileId).Distinct().ToArray();
        if (cookIds.Length != 1) throw new InvalidOperationException("An order can contain food from only one cook.");

        var customerExists = await db.Users.AnyAsync(x => x.Id == customerId && x.Role == UserRole.Customer, ct);
        if (!customerExists) throw new InvalidOperationException("Only customer accounts can place orders.");

        var quantities = request.Items.GroupBy(x => x.FoodId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var order = new Order
        {
            OrderNumber = CreateOrderNumber(),
            CustomerId = customerId,
            CookProfileId = cookIds[0],
            IdempotencyKey = idempotencyKey,
            FulfilmentType = NormalizeFulfilment(request.FulfilmentType),
            DeliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim(),
            Status = OrderStatus.Confirmed
        };

        if (order.FulfilmentType == "Delivery" && string.IsNullOrWhiteSpace(order.DeliveryAddress))
            throw new ArgumentException("Delivery address is required for delivery orders.");

        foreach (var food in foods)
        {
            var qty = quantities[food.Id];
            order.Items.Add(new OrderItem { FoodId = food.Id, FoodName = food.Name, UnitPrice = food.Price, Quantity = qty });
        }
        order.TotalAmount = order.Items.Sum(x => x.UnitPrice * x.Quantity);

        var eventPayload = JsonSerializer.Serialize(new
        {
            order.Id, order.OrderNumber, order.CustomerId, order.CookProfileId, order.TotalAmount,
            Status = order.Status.ToString(), order.CreatedAtUtc
        });
        db.Orders.Add(order);
        db.OutboxMessages.Add(new OutboxMessage { Type = "OrderPlaced", Payload = eventPayload });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var duplicate = await QueryOrders().FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, ct);
            if (duplicate is not null) return Map(duplicate);
            throw;
        }

        return await GetRequiredAsync(order.Id, ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct) =>
        (await QueryOrders().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<OrderDto>> GetCookOrdersAsync(Guid cookUserId, CancellationToken ct)
    {
        var cookId = await db.CookProfiles.Where(x => x.UserId == cookUserId).Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (cookId == Guid.Empty) throw new InvalidOperationException("Cook profile not found.");
        return (await QueryOrders().Where(x => x.CookProfileId == cookId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<OrderDto> UpdateCookOrderStatusAsync(Guid cookUserId, Guid orderId, OrderStatus status, CancellationToken ct)
    {
        var order = await db.Orders.Include(x => x.CookProfile).FirstOrDefaultAsync(x => x.Id == orderId && x.CookProfile.UserId == cookUserId, ct)
            ?? throw new KeyNotFoundException("Order not found.");
        order.TransitionTo(status);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "OrderStatusChanged",
            Payload = JsonSerializer.Serialize(new { order.Id, order.OrderNumber, Status = order.Status.ToString(), order.CustomerId, order.CookProfileId, order.UpdatedAtUtc })
        });
        await db.SaveChangesAsync(ct);
        return await GetRequiredAsync(order.Id, ct);
    }

    private IQueryable<Order> QueryOrders() => db.Orders.AsNoTracking().Include(x => x.CookProfile).ThenInclude(x => x.User).Include(x => x.Items);
    private async Task<OrderDto> GetRequiredAsync(Guid id, CancellationToken ct) => Map(await QueryOrders().SingleAsync(x => x.Id == id, ct));
    private static OrderDto Map(Order o) => new(o.Id, o.OrderNumber, o.CustomerId, o.CookProfileId, o.CookProfile.BusinessName, o.Status, o.TotalAmount, o.FulfilmentType, o.DeliveryAddress, o.CreatedAtUtc, o.Items.Select(i => new OrderItemDto(i.FoodId, i.FoodName, i.UnitPrice, i.Quantity)).ToList());
    private static string NormalizeFulfilment(string? value) => string.Equals(value, "Delivery", StringComparison.OrdinalIgnoreCase) ? "Delivery" : "Pickup";
    private static string CreateOrderNumber() => $"FC-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}
