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

    public async Task<AuthTokens> RegisterAsync(RegisterRequest r, CancellationToken ct)
    {
        var email = Normalize(r.Email); ValidatePassword(r.Password);
        if (string.IsNullOrWhiteSpace(r.DisplayName) || r.DisplayName.Trim().Length > 120) throw new ArgumentException("Display name is required and must be 120 characters or fewer.");
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new InvalidOperationException("An account with this email already exists.");
        var role = string.Equals(r.AccountType, "Cook", StringComparison.OrdinalIgnoreCase) ? UserRole.Cook : UserRole.Customer;
        if (role == UserRole.Cook && (string.IsNullOrWhiteSpace(r.BusinessName) || r.BusinessName.Trim().Length > 160)) throw new ArgumentException("Business name is required for a cook and must be 160 characters or fewer.");
        var user = new User { Email=email, PasswordHash=PasswordHashing.Hash(r.Password), DisplayName=r.DisplayName.Trim(), Role=role };
        if (role == UserRole.Cook) user.CookProfile = new CookProfile { UserId=user.Id, BusinessName=r.BusinessName!.Trim() };
        db.Users.Add(user); await db.SaveChangesAsync(ct); return await IssueAsync(user, ct);
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var email=Normalize(r.Email);
        var user=await db.Users.Include(x=>x.CookProfile).SingleOrDefaultAsync(x=>x.Email==email,ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");
        if(!PasswordHashing.Verify(r.Password,user.PasswordHash)) throw new UnauthorizedAccessException("Invalid email or password.");
        return await IssueAsync(user,ct);
    }

    public async Task<AuthTokens> RefreshAsync(string token, CancellationToken ct)
    {
        var hash=HashToken(token);
        var stored=await db.RefreshTokens.Include(x=>x.User).ThenInclude(x=>x.CookProfile).SingleOrDefaultAsync(x=>x.TokenHash==hash,ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");
        if(stored.RevokedAtUtc is not null || stored.ExpiresAtUtc<=DateTimeOffset.UtcNow) throw new UnauthorizedAccessException("Refresh token expired or revoked.");
        stored.RevokedAtUtc=DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return await IssueAsync(stored.User,ct);
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken ct)
    {
        var hash=HashToken(token); var stored=await db.RefreshTokens.SingleOrDefaultAsync(x=>x.TokenHash==hash,ct);
        if(stored is null || stored.RevokedAtUtc is not null) return;
        stored.RevokedAtUtc=DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
    }

    public Task<AuthUserDto?> GetUserAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(x=>x.Id==id).Select(x=>new AuthUserDto(x.Id,x.Email,x.DisplayName,x.Role.ToString(),x.CookProfile==null?null:x.CookProfile.BusinessName)).SingleOrDefaultAsync(ct);

    private async Task<AuthTokens> IssueAsync(User user, CancellationToken ct)
    {
        if(_jwt.SigningKey.Length<32) throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 characters.");
        var now=DateTimeOffset.UtcNow; var accessExpiry=now.AddMinutes(_jwt.AccessTokenMinutes); var refreshExpiry=now.AddDays(_jwt.RefreshTokenDays);
        var claims=new[] {
            new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),
            new Claim(ClaimTypes.Email,user.Email), new Claim(ClaimTypes.Name,user.DisplayName), new Claim(ClaimTypes.Role,user.Role.ToString())
        };
        var creds=new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),SecurityAlgorithms.HmacSha256);
        var jwt=new JwtSecurityToken(_jwt.Issuer,_jwt.Audience,claims,now.UtcDateTime,accessExpiry.UtcDateTime,creds);
        var access=new JwtSecurityTokenHandler().WriteToken(jwt); var refresh=Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken { UserId=user.Id,TokenHash=HashToken(refresh),ExpiresAtUtc=refreshExpiry });
        await db.SaveChangesAsync(ct);
        var business=user.CookProfile?.BusinessName;
        if(user.Role==UserRole.Cook && business is null) business=await db.CookProfiles.Where(x=>x.UserId==user.Id).Select(x=>x.BusinessName).SingleOrDefaultAsync(ct);
        return new AuthTokens(access,accessExpiry,refresh,refreshExpiry,new AuthUserDto(user.Id,user.Email,user.DisplayName,user.Role.ToString(),business));
    }

    private static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Valid email required.");
        var v = email.Trim().ToLowerInvariant();
        if (v.Length < 3 || v.Length > 320 || !v.Contains('@')) throw new ArgumentException("Valid email required.");
        return v;
    }
    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 10) throw new ArgumentException("Password must be at least 10 characters.");
        if (password.Length > 256) throw new ArgumentException("Password must be 256 characters or fewer.");
    }
    private static string HashToken(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
