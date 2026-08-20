using System.Text.Json;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.CookAtHome;

public sealed class CookAtHomeOrderService(FoodCousinsDbContext db) : ICookAtHomeOrderService
{
    public async Task<CookAtHomeOrderDto> RequestAsync(Guid customerId, string idempotencyKey, CreateCookAtHomeOrderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency-Key is required.");
        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length > 120) throw new ArgumentException("Idempotency-Key is too long.");
        if (request.PeopleCount is < 1 or > 100) throw new ArgumentException("People count must be between 1 and 100.");
        if (string.IsNullOrWhiteSpace(request.Location) || request.Location.Trim().Length > 1000) throw new ArgumentException("Location is required and must be 1000 characters or fewer.");
        if (request.RequestedForUtc <= DateTimeOffset.UtcNow.AddMinutes(30)) throw new ArgumentException("Requested time must be at least 30 minutes in the future.");
        if (string.IsNullOrWhiteSpace(request.ContactName) || request.ContactName.Trim().Length > 120) throw new ArgumentException("Contact name is required and must be 120 characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.ContactPhone) || request.ContactPhone.Trim().Length > 50) throw new ArgumentException("Contact phone is required and must be 50 characters or fewer.");
        if (request.SpecialInstructions?.Length > 2000) throw new ArgumentException("Special instructions must be 2000 characters or fewer.");

        var existing = await Query().FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null) return Map(existing);

        var customerExists = await db.Users.AnyAsync(x => x.Id == customerId && x.Role == UserRole.Customer, ct);
        if (!customerExists) throw new InvalidOperationException("Only customer accounts can request cook-at-home orders.");

        var food = await db.Foods.Include(x => x.CookProfile)
            .SingleOrDefaultAsync(x => x.Id == request.FoodId && x.IsAvailable && x.CookProfile.IsActive, ct)
            ?? throw new KeyNotFoundException("Food is not available.");

        var order = new CookAtHomeOrder
        {
            OrderNumber = CreateOrderNumber(),
            CustomerId = customerId,
            CookProfileId = food.CookProfileId,
            FoodId = food.Id,
            IdempotencyKey = idempotencyKey,
            PeopleCount = request.PeopleCount,
            Location = request.Location.Trim(),
            RequestedForUtc = request.RequestedForUtc.ToUniversalTime(),
            ContactName = request.ContactName.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            SpecialInstructions = string.IsNullOrWhiteSpace(request.SpecialInstructions) ? null : request.SpecialInstructions.Trim(),
            Status = CookAtHomeOrderStatus.Requested
        };

        order.StatusHistory.Add(new CookAtHomeOrderStatusHistory
        {
            Status = CookAtHomeOrderStatus.Requested,
            Note = "Customer submitted cook-at-home request."
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
            var duplicate = await Query().FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, ct);
            if (duplicate is not null) return Map(duplicate);
            throw;
        }

        return await GetRequiredAsync(order.Id, ct);
    }

    public async Task<IReadOnlyList<CookAtHomeOrderDto>> GetCustomerOrdersAsync(Guid customerId, CancellationToken ct) =>
        (await Query().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<CookAtHomeOrderDto>> GetCookOrdersAsync(Guid cookUserId, CancellationToken ct)
    {
        var cookId = await db.CookProfiles.Where(x => x.UserId == cookUserId).Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (cookId == Guid.Empty) throw new KeyNotFoundException("Cook profile not found.");
        return (await Query().Where(x => x.CookProfileId == cookId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)).Select(Map).ToList();
    }

    public async Task<CookAtHomeOrderDto> ConfirmQuoteAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.CookAtHomeOrders.SingleOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException("Cook-at-home order not found.");
        if (order.EstimatedPrice is null) throw new InvalidOperationException("The quote is not ready yet.");
        Transition(order, CookAtHomeOrderStatus.Confirmed, "Customer accepted the quote.");
        AddOutbox("CookAtHomeOrderConfirmed", order);
        await db.SaveChangesAsync(ct);
        return await GetRequiredAsync(order.Id, ct);
    }

    public async Task<CookAtHomeOrderDto> CancelAsync(Guid customerId, Guid orderId, CancellationToken ct)
    {
        var order = await db.CookAtHomeOrders.SingleOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, ct)
            ?? throw new KeyNotFoundException("Cook-at-home order not found.");
        Transition(order, CookAtHomeOrderStatus.Cancelled, "Customer cancelled the request.");
        AddOutbox("CookAtHomeOrderStatusChanged", order);
        await db.SaveChangesAsync(ct);
        return await GetRequiredAsync(order.Id, ct);
    }

    public async Task<CookAtHomeOrderDto> UpdateCookStatusAsync(Guid cookUserId, Guid orderId, CookAtHomeOrderStatus status, CancellationToken ct)
    {
        if (status is not (CookAtHomeOrderStatus.AcceptedByCook or CookAtHomeOrderStatus.Rejected or CookAtHomeOrderStatus.Preparing or CookAtHomeOrderStatus.Completed))
            throw new ArgumentException("Cook cannot set that status.");

        var order = await db.CookAtHomeOrders.Include(x => x.CookProfile)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.CookProfile.UserId == cookUserId, ct)
            ?? throw new KeyNotFoundException("Cook-at-home order not found.");

        Transition(order, status, $"Cook changed status to {status}.");
        AddOutbox("CookAtHomeOrderStatusChanged", order);
        await db.SaveChangesAsync(ct);
        return await GetRequiredAsync(order.Id, ct);
    }

    private void Transition(CookAtHomeOrder order, CookAtHomeOrderStatus next, string note)
    {
        order.TransitionTo(next);
        db.CookAtHomeOrderStatusHistory.Add(new CookAtHomeOrderStatusHistory
        {
            CookAtHomeOrderId = order.Id,
            Status = next,
            Note = note
        });
    }

    private void AddOutbox(string type, CookAtHomeOrder order) => db.OutboxMessages.Add(new OutboxMessage
    {
        Type = type,
        Payload = JsonSerializer.Serialize(new
        {
            order.Id,
            order.OrderNumber,
            Status = order.Status.ToString(),
            order.CustomerId,
            order.CookProfileId,
            order.FoodId,
            order.EstimatedPrice,
            order.UpdatedAtUtc
        })
    });

    private IQueryable<CookAtHomeOrder> Query() => db.CookAtHomeOrders.AsNoTracking()
        .Include(x => x.CookProfile).ThenInclude(x => x.User)
        .Include(x => x.Food);

    private async Task<CookAtHomeOrderDto> GetRequiredAsync(Guid id, CancellationToken ct) => Map(await Query().SingleAsync(x => x.Id == id, ct));

    private static CookAtHomeOrderDto Map(CookAtHomeOrder x) => new(
        x.Id,
        x.OrderNumber,
        x.CustomerId,
        x.CookProfileId,
        x.CookProfile.BusinessName,
        x.FoodId,
        x.Food.Name,
        x.PeopleCount,
        x.Location,
        x.RequestedForUtc,
        x.ContactName,
        x.ContactPhone,
        x.SpecialInstructions,
        x.Status,
        x.EstimatedPrice,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);

    private static string CreateOrderNumber() => $"FCH-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
}
