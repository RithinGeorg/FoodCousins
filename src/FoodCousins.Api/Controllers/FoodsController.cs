using FoodCousins.Application.Common;
using FoodCousins.Application.Foods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/foods")]
public sealed class FoodsController(IFoodService foods) : ControllerBase
{
    [AllowAnonymous, HttpGet]
    public async Task<ActionResult<IReadOnlyList<FoodDto>>> Get([FromQuery] string? q, [FromQuery] string? cuisine, CancellationToken ct) => Ok(await foods.SearchAsync(q, cuisine, ct));

    [AllowAnonymous, HttpGet("{id:guid}/cousins")]
    public async Task<ActionResult<IReadOnlyList<FoodCousinDto>>> Cousins(Guid id, CancellationToken ct) => Ok(await foods.GetCousinsAsync(id, ct));

    [AllowAnonymous, HttpGet("{id:guid}")]
    public async Task<ActionResult<FoodDto>> GetById(Guid id, CancellationToken ct)
    {
        var food = await foods.GetByIdAsync(id, ct); return food is null ? NotFound() : Ok(food);
    }

    [Authorize(Roles = "Cook"), HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<FoodDto>>> Mine(CancellationToken ct) => Ok(await foods.GetCookFoodsAsync(CurrentUser.GetRequiredUserId(User), ct));

    [Authorize(Roles = "Cook"), HttpPost]
    public async Task<ActionResult<FoodDto>> Create(UpsertFoodRequest request, CancellationToken ct)
    {
        var food = await foods.CreateAsync(CurrentUser.GetRequiredUserId(User), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = food.Id }, food);
    }

    [Authorize(Roles = "Cook"), HttpPut("{id:guid}")]
    public Task<FoodDto> Update(Guid id, UpsertFoodRequest request, CancellationToken ct) => foods.UpdateAsync(CurrentUser.GetRequiredUserId(User), id, request, ct);

    [Authorize(Roles = "Cook"), HttpPost("{id:guid}/image")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<FoodDto>> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > 5_000_000) return BadRequest("Image must be between 1 byte and 5 MB.");
        await using var stream = file.OpenReadStream();
        return Ok(await foods.SetImageAsync(CurrentUser.GetRequiredUserId(User), id, stream, file.ContentType, file.FileName, ct));
    }
}
