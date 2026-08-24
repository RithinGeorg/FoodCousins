using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Delivery;
using FoodCousins.Application.Foods;
using FoodCousins.Domain.Enums;

namespace FoodCousins.Application.Admin;

public sealed record AdminDashboardDto(
    int FoodCount,
    int AiReviewPendingCount,
    int ActiveDeliveryAreaCount,
    int OpenOrderCount,
    int OutForDeliveryCount,
    int DeliveredTodayCount,
    decimal RevenueToday);

public sealed record AdminOrderStatusRequest(CookAtHomeOrderStatus Status, string? Note);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminUserUpdateRequest(UserRole Role, bool IsActive);

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FoodSummaryDto>> GetFoodsAsync(CancellationToken cancellationToken);
    Task<FoodSummaryDto> UpdateFoodAsync(Guid foodId, AdminFoodUpdateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryAreaDto>> GetDeliveryAreasAsync(CancellationToken cancellationToken);
    Task<DeliveryAreaDto> UpsertDeliveryAreaAsync(UpsertDeliveryAreaRequest request, CancellationToken cancellationToken);
    Task SetDeliveryAreaActiveAsync(Guid id, bool active, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<AdminUserDto> UpdateUserAsync(Guid userId, AdminUserUpdateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CookAtHomeOrderDto>> GetOrdersAsync(CancellationToken cancellationToken);
    Task<CookAtHomeOrderDto> UpdateOrderStatusAsync(Guid orderId, AdminOrderStatusRequest request, CancellationToken cancellationToken);
}
