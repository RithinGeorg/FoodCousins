using System.Security.Claims;
namespace FoodCousins.Application.Common;
public static class CurrentUser
{
    public static Guid GetRequiredUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Missing user id claim.");
    }
}
