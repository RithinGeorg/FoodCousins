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
    [Authorize(Roles = "Customer"), HttpPost]
    public async Task<ActionResult<CookAtHomeOrderDto>> RequestCook(CreateCookAtHomeOrderRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values.FirstOrDefault()))
            return BadRequest("Idempotency-Key header is required.");

        return Ok(await orders.RequestAsync(CurrentUser.GetRequiredUserId(User), values.First()!, request, ct));
    }

    [Authorize(Roles = "Customer"), HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<CookAtHomeOrderDto>>> Mine(CancellationToken ct) =>
        Ok(await orders.GetCustomerOrdersAsync(CurrentUser.GetRequiredUserId(User), ct));

    [Authorize(Roles = "Customer"), HttpPost("{id:guid}/confirm-quote")]
    public async Task<ActionResult<CookAtHomeOrderDto>> ConfirmQuote(Guid id, CancellationToken ct) =>
        Ok(await orders.ConfirmQuoteAsync(CurrentUser.GetRequiredUserId(User), id, ct));

    [Authorize(Roles = "Customer"), HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<CookAtHomeOrderDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await orders.CancelAsync(CurrentUser.GetRequiredUserId(User), id, ct));

    [Authorize(Roles = "Cook"), HttpGet("cook")]
    public async Task<ActionResult<IReadOnlyList<CookAtHomeOrderDto>>> Cook(CancellationToken ct) =>
        Ok(await orders.GetCookOrdersAsync(CurrentUser.GetRequiredUserId(User), ct));

    [Authorize(Roles = "Cook"), HttpPut("{id:guid}/status")]
    public async Task<ActionResult<CookAtHomeOrderDto>> UpdateStatus(Guid id, UpdateCookAtHomeStatusRequest request, CancellationToken ct) =>
        Ok(await orders.UpdateCookStatusAsync(CurrentUser.GetRequiredUserId(User), id, request.Status, ct));
}
