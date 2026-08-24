using FoodCousins.Application.Foods;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/foods")]
public sealed class FoodsController(IFoodService foods) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FoodSummaryDto>>> GetPublished(CancellationToken ct) =>
        Ok(await foods.GetPublishedAsync(ct));

    [HttpGet("search")]
    public async Task<ActionResult<FoodSearchResultDto>> Search([FromQuery] string q, CancellationToken ct) =>
        Ok(await foods.SearchAsync(q, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FoodDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var food = await foods.GetByIdAsync(id, ct);
        return food is null ? NotFound() : Ok(food);
    }

    [HttpGet("{id:guid}/cousins")]
    public async Task<ActionResult<IReadOnlyList<FoodCousinDto>>> GetCousins(Guid id, CancellationToken ct) =>
        Ok(await foods.GetCousinsAsync(id, ct));
}
