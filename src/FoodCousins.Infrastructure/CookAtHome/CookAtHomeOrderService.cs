using System.Text.Json;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Delivery;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.CookAtHome;

public sealed class CookAtHomeOrderService(FoodCousinsDbContext db) : ICookAtHomeOrderService
{
    public async Task<CookAtHomeOrderDto> RequestAsync(
        Guid customerId,
        string idempotencyKey,
        CreateCookAtHomeOrderRequest request,
        CancellationToken ct)
    {
        ValidateRequest(idempotencyKey, request);
        idempotencyKey = idempotencyKey.Trim();

        var duplicate = await Query().FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey,
            ct);
        if (duplicate is not null) return Map(duplicate);

        var customer = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer account not found.");
        if (!customer.IsActive)
        {
            throw new UnauthorizedAccessException(
                "Customer account is disabled.");
        }
        if (customer.Role != UserRole.Customer)
            throw new InvalidOperationException("Only customer accounts can create Cook at Home orders.");


        var food = await db.Foods
            .Include(x => x.Recipe).ThenInclude(x => x!.Steps)
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .SingleOrDefaultAsync(
                x => x.Id == request.FoodId && x.IsPublished && x.IsCookAtHomeEnabled,
                ct)
            ?? throw new KeyNotFoundException("This food is not currently available for Cook at Home.");
        if (food.KitPricePerPerson <= 0)
        {
            throw new InvalidOperationException(
                "Cook at Home pricing has not been configured for this food.");
        }

        if (food.Recipe is null || food.Ingredients.Count == 0)
            throw new InvalidOperationException("This food does not have a complete recipe and ingredient list.");

        var postcode = DeliveryAreaService.NormalizePostcode(request.Postcode);
        var deliveryArea = await db.DeliveryAreas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Postcode == postcode && x.IsActive, ct)
            ?? throw new ArgumentException("Cook at Home delivery is not available for this postcode yet.");

        var order = new CookAtHomeOrder
        {
            OrderNumber = CreateOrderNumber(),
            CustomerId = customerId,
            FoodId = food.Id,
            FoodName = food.Name,
            IdempotencyKey = idempotencyKey,
            PeopleCount = request.PeopleCount,
            Postcode = deliveryArea.Postcode,
            Suburb = deliveryArea.Suburb,
            DeliveryAddress = request.DeliveryAddress.Trim(),
            RequestedDeliveryUtc = request.RequestedDeliveryUtc.ToUniversalTime(),
            ContactName = request.ContactName.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            SpecialInstructions = string.IsNullOrWhiteSpace(request.SpecialInstructions)
                ? null
                : request.SpecialInstructions.Trim(),
            KitPricePerPerson = food.KitPricePerPerson,
            DeliveryFee = deliveryArea.DeliveryFee,
            MinimumOrderAmount = deliveryArea.MinimumOrderAmount,
            DeliveryEstimateMinutes = deliveryArea.EstimatedDeliveryMinutes,
            RecipeTitle = food.Recipe.Title,
            RecipeInstructions = string.Join(
                Environment.NewLine,
                food.Recipe.Steps.OrderBy(x => x.StepNumber)
                    .Select(x => $"{x.StepNumber}. {x.Instruction}")),
            Status = CookAtHomeOrderStatus.Requested
        };

        foreach (var ingredient in food.Ingredients)
        {
            order.Ingredients.Add(new CookAtHomeOrderIngredient
            {
                IngredientName = ingredient.Ingredient.Name,
                Quantity = ingredient.QuantityPerServing * request.PeopleCount,
                Unit = ingredient.Unit,
                IsOptional = ingredient.IsOptional,
                Notes = ingredient.Notes
            });
        }

        order.StatusHistory.Add(new CookAtHomeOrderStatusHistory
        {
            Status = CookAtHomeOrderStatus.Requested,
            Note = "Customer submitted an ingredient-kit order."
        });

        db.CookAtHomeOrders.Add(order);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "CookAtHomeOrderRequested",
            Payload = JsonSerializer.Serialize(new CookAtHomeOrderRequestedEvent(order.Id))
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var concurrentDuplicate = await Query().FirstOrDefaultAsync(
                x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey,
                ct);
            if (concurrentDuplicate is not null) return Map(concurrentDuplicate);
            throw;
        }

        return await GetRequiredAsync(order.Id, ct);
    }

    public async Task<IReadOnlyList<CookAtHomeOrderDto>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct) =>
        (await Query().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct))
        .Select(Map)
        .ToList();

    public async Task<CookAtHomeOrderDto?> GetCustomerOrderAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await Query().SingleOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, ct);
        return order is null ? null : Map(order);
    }

    public async Task<CookAtHomeOrderDto> CancelAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.CookAtHomeOrders
            .Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException("Cook at Home order not found.");

        order.TransitionTo(CookAtHomeOrderStatus.Cancelled, "Cancelled by customer.");
        await db.SaveChangesAsync(ct);
        return await GetRequiredAsync(order.Id, ct);
    }

    internal IQueryable<CookAtHomeOrder> Query() => db.CookAtHomeOrders.AsNoTracking()
        .Include(x => x.Food)
        .Include(x => x.Ingredients)
        .Include(x => x.StatusHistory);

    internal async Task<CookAtHomeOrderDto> GetRequiredAsync(Guid id, CancellationToken ct)
    {
        var order = await Query().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Cook at Home order not found.");
        return Map(order);
    }

    internal static CookAtHomeOrderDto Map(CookAtHomeOrder x) => new(
        x.Id,
        x.OrderNumber,
        x.FoodId,
        x.FoodName,
        x.PeopleCount,
        x.Postcode,
        x.Suburb,
        x.DeliveryAddress,
        x.RequestedDeliveryUtc,
        x.EstimatedDeliveryUtc,
        x.ContactName,
        x.ContactPhone,
        x.SpecialInstructions,
        x.Status,
        x.Subtotal,
        x.DeliveryFee,
        x.TotalAmount,
        x.RecipeTitle,
        x.RecipeInstructions,
        x.EmailSentAtUtc,
        x.CreatedAtUtc,
        x.UpdatedAtUtc,
        x.Ingredients.OrderBy(i => i.IngredientName)
            .Select(i => new CookAtHomeOrderIngredientDto(i.IngredientName, i.Quantity, i.Unit, i.IsOptional, i.Notes))
            .ToList(),
        x.StatusHistory.OrderBy(h => h.CreatedAtUtc)
            .Select(h => new CookAtHomeOrderStatusHistoryDto(h.Status, h.Note, h.CreatedAtUtc))
            .ToList());

    private static void ValidateRequest(string idempotencyKey, CreateCookAtHomeOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency-Key is required.");
        if (idempotencyKey.Trim().Length > 120) throw new ArgumentException("Idempotency-Key is too long.");
        if (request.PeopleCount is < 1 or > 30) throw new ArgumentException("People count must be between 1 and 30.");
        _ = DeliveryAreaService.NormalizePostcode(request.Postcode);
        if (string.IsNullOrWhiteSpace(request.DeliveryAddress) || request.DeliveryAddress.Trim().Length > 1000)
            throw new ArgumentException("Delivery address is required and must be 1000 characters or fewer.");
        if (request.RequestedDeliveryUtc <= DateTimeOffset.UtcNow.AddMinutes(30))
            throw new ArgumentException("Requested delivery time must be at least 30 minutes in the future.");
        if (string.IsNullOrWhiteSpace(request.ContactName) || request.ContactName.Trim().Length > 120)
            throw new ArgumentException("Contact name is required and must be 120 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.ContactPhone) || request.ContactPhone.Trim().Length > 50)
            throw new ArgumentException("Contact phone is required and must be 50 characters or fewer.");
        if (request.SpecialInstructions?.Length > 2000)
            throw new ArgumentException("Special instructions must be 2000 characters or fewer.");
    }

    private static string CreateOrderNumber() => $"FCK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
}
