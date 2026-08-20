using FoodCousins.Application.Auth;
using FoodCousins.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodCousins.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var tokens = await auth.RegisterAsync(request, ct); SetCookies(tokens); return Ok(tokens.User);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthUserDto>> Login(LoginRequest request, CancellationToken ct)
    {
        var tokens = await auth.LoginAsync(request, ct); SetCookies(tokens); return Ok(tokens.User);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthUserDto>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue("fc_refresh", out var token) || string.IsNullOrWhiteSpace(token)) return Unauthorized();
        var tokens = await auth.RefreshAsync(token, ct); SetCookies(tokens); return Ok(tokens.User);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken ct)
    {
        var user = await auth.GetUserAsync(CurrentUser.GetRequiredUserId(User), ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue("fc_refresh", out var token) && !string.IsNullOrWhiteSpace(token)) await auth.RevokeRefreshTokenAsync(token, ct);
        DeleteCookies(); return NoContent();
    }

    private void SetCookies(AuthTokens tokens)
    {
        var sameSite = configuration.GetValue<bool>("Auth:CrossSiteCookies") ? SameSiteMode.None : SameSiteMode.Lax;
        Response.Cookies.Append("fc_access", tokens.AccessToken, CookieOptions(tokens.AccessTokenExpiresAtUtc, sameSite, "/"));
        Response.Cookies.Append("fc_refresh", tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAtUtc, sameSite, "/api/v1/auth"));
    }

    private CookieOptions CookieOptions(DateTimeOffset expires, SameSiteMode sameSite, string path) => new()
    {
        HttpOnly = true,
        Secure = !configuration.GetValue<bool>("Auth:AllowInsecureCookies"),
        SameSite = sameSite,
        Expires = expires,
        Path = path
    };

    private void DeleteCookies()
    {
        Response.Cookies.Delete("fc_access", new CookieOptions { Path = "/" });
        Response.Cookies.Delete("fc_refresh", new CookieOptions { Path = "/api/v1/auth" });
    }
}
