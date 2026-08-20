using FoodCousins.Application.Common;
using FoodCousins.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [Authorize(Roles = "Customer"), HttpPost]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values.FirstOrDefault())) return BadRequest("Idempotency-Key header is required.");
        var order = await orders.PlaceAsync(CurrentUser.GetRequiredUserId(User), values.First()!, request, ct);
        return Ok(order);
    }

    [Authorize(Roles = "Customer"), HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Mine(CancellationToken ct) => Ok(await orders.GetCustomerOrdersAsync(CurrentUser.GetRequiredUserId(User), ct));

    [Authorize(Roles = "Cook"), HttpGet("cook")]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Cook(CancellationToken ct) => Ok(await orders.GetCookOrdersAsync(CurrentUser.GetRequiredUserId(User), ct));

    [Authorize(Roles = "Cook"), HttpPut("{id:guid}/status")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(Guid id, UpdateOrderStatusRequest request, CancellationToken ct) =>
        Ok(await orders.UpdateCookOrderStatusAsync(CurrentUser.GetRequiredUserId(User), id, request.Status, ct));
}
