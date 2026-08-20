namespace FoodCousins.Application.Auth;
public sealed record RegisterRequest(string Email, string Password, string DisplayName, string AccountType = "Customer", string? BusinessName = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthUserDto(Guid Id, string Email, string DisplayName, string Role, string? BusinessName);
public sealed record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc, AuthUserDto User);
