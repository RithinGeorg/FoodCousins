using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FoodCousins.Application.Auth;
using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoodCousins.Infrastructure.Auth;

internal sealed class AuthService(FoodCousinsDbContext db, IOptions<JwtOptions> options) : IAuthService
{
    private readonly JwtOptions _jwt = options.Value;

    public async Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120)
            throw new ArgumentException("Display name is required and must be 120 characters or fewer.");

        if (await db.Users.AnyAsync(x => x.Email == email, ct))
            throw new InvalidOperationException("An account with this email already exists.");

        // Public registration can never create an Admin account.
        var user = new User
        {
            Email = email,
            PasswordHash = PasswordHashing.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Role = UserRole.Customer
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!PasswordHashing.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");
        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account is disabled.");

        return await IssueAsync(user, ct);
    }

    public async Task<AuthTokens> RefreshAsync(string token, CancellationToken ct)
    {
        var hash = HashToken(token);
        var stored = await db.RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired or revoked.");
        if (!stored.User.IsActive)
            throw new UnauthorizedAccessException("This account is disabled.");

        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await IssueAsync(stored.User, ct);
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct)
    {
        var hash = HashToken(token);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAtUtc is not null) return;

        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new AuthUserDto(x.Id, x.Email, x.DisplayName, x.Role.ToString()))
            .SingleOrDefaultAsync(ct);

    private async Task<AuthTokens> IssueAsync(User user, CancellationToken ct)
    {
        if (_jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 characters.");

        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(_jwt.AccessTokenMinutes);
        var refreshExpiry = now.AddDays(_jwt.RefreshTokenDays);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            _jwt.Issuer,
            _jwt.Audience,
            claims,
            now.UtcDateTime,
            accessExpiry.UtcDateTime,
            credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = refreshExpiry
        });

        await db.SaveChangesAsync(ct);

        return new AuthTokens(
            accessToken,
            accessExpiry,
            refreshToken,
            refreshExpiry,
            new AuthUserDto(user.Id, user.Email, user.DisplayName, user.Role.ToString()));
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Valid email required.");
        var value = email.Trim().ToLowerInvariant();
        if (value.Length is < 3 or > 320 || !value.Contains('@')) throw new ArgumentException("Valid email required.");
        return value;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 10)
            throw new ArgumentException("Password must be at least 10 characters.");
        if (password.Length > 256)
            throw new ArgumentException("Password must be 256 characters or fewer.");
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
