using FoodCousins.Application.Delivery;
using FoodCousins.Domain.Entities;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Delivery;

internal sealed class DeliveryAreaService(FoodCousinsDbContext db) : IDeliveryAreaService
{
    public async Task<DeliveryAvailabilityDto> CheckAsync(string postcode, CancellationToken ct)
    {
        var value = NormalizePostcode(postcode);
        var area = await db.DeliveryAreas.AsNoTracking().SingleOrDefaultAsync(x => x.Postcode == value, ct);

        return area is null
            ? new DeliveryAvailabilityDto(value, null, false, null, null, null)
            : new DeliveryAvailabilityDto(
                area.Postcode,
                area.Suburb,
                area.IsActive,
                area.IsActive ? area.DeliveryFee : null,
                area.IsActive ? area.MinimumOrderAmount : null,
                area.IsActive ? area.EstimatedDeliveryMinutes : null);
    }

    public async Task<IReadOnlyList<DeliveryAreaDto>> GetAllAsync(CancellationToken ct) =>
        (await db.DeliveryAreas.AsNoTracking().OrderBy(x => x.Postcode).ToListAsync(ct))
            .Select(Map)
            .ToList();

    public async Task<DeliveryAreaDto> UpsertAsync(UpsertDeliveryAreaRequest request, CancellationToken ct)
    {
        Validate(request);
        var postcode = NormalizePostcode(request.Postcode);
        var area = await db.DeliveryAreas.SingleOrDefaultAsync(x => x.Postcode == postcode, ct);

        if (area is null)
        {
            area = new DeliveryArea { Postcode = postcode, Suburb = request.Suburb.Trim() };
            db.DeliveryAreas.Add(area);
        }

        area.Suburb = request.Suburb.Trim();
        area.IsActive = request.IsActive;
        area.DeliveryFee = request.DeliveryFee;
        area.MinimumOrderAmount = request.MinimumOrderAmount;
        area.EstimatedDeliveryMinutes = request.EstimatedDeliveryMinutes;
        area.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Map(area);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct)
    {
        var area = await db.DeliveryAreas.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Delivery area not found.");

        area.IsActive = active;
        area.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    internal static DeliveryAreaDto Map(DeliveryArea x) => new(
        x.Id,
        x.Postcode,
        x.Suburb,
        x.IsActive,
        x.DeliveryFee,
        x.MinimumOrderAmount,
        x.EstimatedDeliveryMinutes);

    internal static string NormalizePostcode(string postcode)
    {
        if (string.IsNullOrWhiteSpace(postcode))
            throw new ArgumentException("Postcode is required.");

        var value = postcode
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (value.Length != 4 || !value.All(char.IsDigit))
            throw new ArgumentException(
                "Australian postcode must contain exactly 4 digits.");

        return value;
    }

    private static void Validate(UpsertDeliveryAreaRequest request)
    {
        _ = NormalizePostcode(request.Postcode);
        if (string.IsNullOrWhiteSpace(request.Suburb) || request.Suburb.Trim().Length > 120)
            throw new ArgumentException("Suburb is required and must be 120 characters or fewer.");
        if (request.DeliveryFee is < 0 or > 1000) throw new ArgumentException("Delivery fee is invalid.");
        if (request.MinimumOrderAmount is < 0 or > 10000) throw new ArgumentException("Minimum order amount is invalid.");
        if (request.EstimatedDeliveryMinutes is < 15 or > 10080) throw new ArgumentException("Estimated delivery minutes must be between 15 and 10080.");
    }
}
