using FoodCousins.Application.Admin;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Delivery;
using FoodCousins.Application.Foods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> Dashboard(CancellationToken ct) =>
        Ok(await admin.GetDashboardAsync(ct));

    [HttpGet("foods")]
    public async Task<ActionResult<IReadOnlyList<FoodSummaryDto>>> Foods(CancellationToken ct) =>
        Ok(await admin.GetFoodsAsync(ct));

    [HttpPut("foods/{id:guid}")]
    public async Task<ActionResult<FoodSummaryDto>> UpdateFood(
        Guid id,
        AdminFoodUpdateRequest request,
        CancellationToken ct) =>
        Ok(await admin.UpdateFoodAsync(id, request, ct));

    [HttpGet("delivery-areas")]
    public async Task<ActionResult<IReadOnlyList<DeliveryAreaDto>>> DeliveryAreas(CancellationToken ct) =>
        Ok(await admin.GetDeliveryAreasAsync(ct));

    [HttpPut("delivery-areas")]
    public async Task<ActionResult<DeliveryAreaDto>> UpsertDeliveryArea(
        UpsertDeliveryAreaRequest request,
        CancellationToken ct) =>
        Ok(await admin.UpsertDeliveryAreaAsync(request, ct));

    [HttpPut("delivery-areas/{id:guid}/active")]
    public async Task<IActionResult> SetDeliveryAreaActive(Guid id, [FromQuery] bool value, CancellationToken ct)
    {
        await admin.SetDeliveryAreaActiveAsync(id, value, ct);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> Users(CancellationToken ct) =>
        Ok(await admin.GetUsersAsync(ct));

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(Guid id, AdminUserUpdateRequest request, CancellationToken ct) =>
        Ok(await admin.UpdateUserAsync(id, request, ct));

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<CookAtHomeOrderDto>>> Orders(CancellationToken ct) =>
        Ok(await admin.GetOrdersAsync(ct));

    [HttpPut("orders/{id:guid}/status")]
    public async Task<ActionResult<CookAtHomeOrderDto>> UpdateOrderStatus(
        Guid id,
        AdminOrderStatusRequest request,
        CancellationToken ct) =>
        Ok(await admin.UpdateOrderStatusAsync(id, request, ct));
}
