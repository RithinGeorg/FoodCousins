using FoodCousins.Application.Common;
using FoodCousins.Application.CookAtHome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/cook-at-home-orders")]
[Authorize]
public sealed class CookAtHomeOrdersController(ICookAtHomeOrderService orders) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CookAtHomeOrderDto>> Create(
        CreateCookAtHomeOrderRequest request,
        CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
            return BadRequest(new ProblemDetails { Title = "Idempotency-Key header is required." });

        var order = await orders.RequestAsync(CurrentUser.GetRequiredUserId(User), key.ToString(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<CookAtHomeOrderDto>>> Mine(CancellationToken ct) =>
        Ok(await orders.GetCustomerOrdersAsync(CurrentUser.GetRequiredUserId(User), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CookAtHomeOrderDto>> GetById(Guid id, CancellationToken ct)
    {
        var order = await orders.GetCustomerOrderAsync(CurrentUser.GetRequiredUserId(User), id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<CookAtHomeOrderDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await orders.CancelAsync(CurrentUser.GetRequiredUserId(User), id, ct));
}
