using FoodCousins.Application.Delivery;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/delivery-areas")]
public sealed class DeliveryAreasController(IDeliveryAreaService deliveryAreas) : ControllerBase
{
    [HttpGet("check/{postcode}")]
    public async Task<ActionResult<DeliveryAvailabilityDto>> Check(string postcode, CancellationToken ct) =>
        Ok(await deliveryAreas.CheckAsync(postcode, ct));
}
