using FoodCousins.Domain.Entities;
using FoodCousins.Domain.Enums;
using FoodCousins.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(
        FoodCousinsDbContext db,
        IConfiguration configuration,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!await db.DeliveryAreas.AnyAsync(ct))
        {
            db.DeliveryAreas.AddRange(
                new DeliveryArea
                {
                    Postcode = "4227",
                    Suburb = "Varsity Lakes",
                    IsActive = true,
                    DeliveryFee = 5m,
                    MinimumOrderAmount = 20m,
                    EstimatedDeliveryMinutes = 120
                },
                new DeliveryArea
                {
                    Postcode = "4226",
                    Suburb = "Robina",
                    IsActive = true,
                    DeliveryFee = 5m,
                    MinimumOrderAmount = 20m,
                    EstimatedDeliveryMinutes = 120
                });
        }

        // There is intentionally no public API that can create an Admin. The first
        // administrator can be bootstrapped from server-side configuration/Key Vault.
        // Once an Admin exists, the admin dashboard can promote/demote other users.
        var adminEmail = configuration["AdminBootstrap:Email"]?.Trim().ToLowerInvariant();
        var adminPassword = configuration["AdminBootstrap:Password"];
        var adminDisplayName = configuration["AdminBootstrap:DisplayName"]?.Trim();

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            if (adminEmail.Length > 320 || !adminEmail.Contains('@'))
                throw new InvalidOperationException("AdminBootstrap:Email is invalid.");
            if (adminPassword.Length is < 12 or > 256)
                throw new InvalidOperationException("AdminBootstrap:Password must be between 12 and 256 characters.");

            var existingAdmin = await db.Users.SingleOrDefaultAsync(x => x.Email == adminEmail, ct);
            if (existingAdmin is null)
            {
                db.Users.Add(new User
                {
                    Email = adminEmail,
                    PasswordHash = PasswordHashing.Hash(adminPassword),
                    DisplayName = string.IsNullOrWhiteSpace(adminDisplayName) ? "FoodCousins Admin" : adminDisplayName,
                    Role = UserRole.Admin,
                    IsActive = true
                });

                logger?.LogInformation("Initial FoodCousins administrator created from bootstrap configuration. Email={Email}", adminEmail);
            }
            else if (existingAdmin.Role != UserRole.Admin)
            {
                existingAdmin.Role = UserRole.Admin;
                existingAdmin.IsActive = true;
                logger?.LogInformation("Existing FoodCousins account promoted to administrator from bootstrap configuration. Email={Email}", adminEmail);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
