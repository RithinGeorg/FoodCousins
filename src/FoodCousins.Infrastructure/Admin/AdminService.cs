using FoodCousins.Application.Admin;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Delivery;
using FoodCousins.Application.Foods;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.CookAtHome;
using FoodCousins.Infrastructure.Delivery;
using FoodCousins.Infrastructure.Foods;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Admin;

internal sealed class AdminService(FoodCousinsDbContext db, IDeliveryAreaService deliveryAreas) : IAdminService
{
    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);
        var openStatuses = new[]
        {
            CookAtHomeOrderStatus.Requested,
            CookAtHomeOrderStatus.Processing,
            CookAtHomeOrderStatus.Confirmed,
            CookAtHomeOrderStatus.Preparing,
            CookAtHomeOrderStatus.ReadyForDelivery,
            CookAtHomeOrderStatus.OutForDelivery
        };

        return new AdminDashboardDto(
            await db.Foods.CountAsync(ct),
            await db.Foods.CountAsync(x => x.AiGenerated && !x.AiReviewed, ct),
            await db.DeliveryAreas.CountAsync(x => x.IsActive, ct),
            await db.CookAtHomeOrders.CountAsync(x => openStatuses.Contains(x.Status), ct),
            await db.CookAtHomeOrders.CountAsync(x => x.Status == CookAtHomeOrderStatus.OutForDelivery, ct),
            await db.CookAtHomeOrders.CountAsync(
                x => x.Status == CookAtHomeOrderStatus.Delivered && x.UpdatedAtUtc >= today && x.UpdatedAtUtc < tomorrow,
                ct),
            await db.CookAtHomeOrders
                .Where(x => x.Status == CookAtHomeOrderStatus.Delivered && x.UpdatedAtUtc >= today && x.UpdatedAtUtc < tomorrow)
                .SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0m);
    }

    public async Task<IReadOnlyList<FoodSummaryDto>> GetFoodsAsync(CancellationToken ct) =>
        (await db.Foods.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct))
            .Select(FoodService.MapSummary)
            .ToList();

    public async Task<FoodSummaryDto> UpdateFoodAsync(Guid foodId, AdminFoodUpdateRequest request, CancellationToken ct)
    {
        if (request.KitPricePerPerson is < 0 or > 10000)
        {
            throw new ArgumentException(
                "Kit price per person must be between 0 and 10000.");
        }

        if (request.IsCookAtHomeEnabled &&
            request.KitPricePerPerson <= 0)
        {
            throw new ArgumentException(
                "A positive kit price must be configured before Cook at Home can be enabled.");
        }

        var food = await db.Foods.SingleOrDefaultAsync(x => x.Id == foodId, ct)
            ?? throw new KeyNotFoundException("Food not found.");

        food.IsPublished = request.IsPublished;
        food.AiReviewed = request.AiReviewed;
        food.IsCookAtHomeEnabled = request.IsCookAtHomeEnabled;
        food.KitPricePerPerson = request.KitPricePerPerson;
        food.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return FoodService.MapSummary(food);
    }

    public async Task<IReadOnlyList<DeliveryAreaDto>> GetDeliveryAreasAsync(CancellationToken ct) =>
        (await db.DeliveryAreas.AsNoTracking().OrderBy(x => x.Postcode).ToListAsync(ct))
            .Select(DeliveryAreaService.Map)
            .ToList();

    public Task<DeliveryAreaDto> UpsertDeliveryAreaAsync(UpsertDeliveryAreaRequest request, CancellationToken ct) =>
        deliveryAreas.UpsertAsync(request, ct);

    public Task SetDeliveryAreaActiveAsync(Guid id, bool active, CancellationToken ct) =>
        deliveryAreas.SetActiveAsync(id, active, ct);


    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken ct) =>
        (await db.Users.AsNoTracking().OrderBy(x => x.Email).Take(1000).ToListAsync(ct))
            .Select(x => new AdminUserDto(x.Id, x.Email, x.DisplayName, x.Role, x.IsActive, x.CreatedAtUtc))
            .ToList();

    public async Task<AdminUserDto> UpdateUserAsync(Guid userId, AdminUserUpdateRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new AdminUserDto(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<CookAtHomeOrderDto>> GetOrdersAsync(CancellationToken ct)
    {
        var orders = await db.CookAtHomeOrders.AsNoTracking()
            .Include(x => x.Food)
            .Include(x => x.Ingredients)
            .Include(x => x.StatusHistory)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);

        return orders.Select(CookAtHomeOrderService.Map).ToList();
    }

    public async Task<CookAtHomeOrderDto> UpdateOrderStatusAsync(
        Guid orderId,
        AdminOrderStatusRequest request,
        CancellationToken ct)
    {
        var order = await db.CookAtHomeOrders
            .Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == orderId, ct)
            ?? throw new KeyNotFoundException("Cook at Home order not found.");

        if (order.Status != request.Status)
            order.TransitionTo(request.Status, request.Note ?? $"Status changed by admin to {request.Status}.");

        await db.SaveChangesAsync(ct);

        var mapped = await db.CookAtHomeOrders.AsNoTracking()
            .Include(x => x.Food)
            .Include(x => x.Ingredients)
            .Include(x => x.StatusHistory)
            .SingleAsync(x => x.Id == order.Id, ct);

        return CookAtHomeOrderService.Map(mapped);
    }
}
