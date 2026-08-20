using FoodCousins.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Persistence;

public sealed class FoodCousinsDbContext(DbContextOptions<FoodCousinsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CookProfile> CookProfiles => Set<CookProfile>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<CookAtHomeOrder> CookAtHomeOrders => Set<CookAtHomeOrder>();
    public DbSet<CookAtHomeOrderStatusHistory> CookAtHomeOrderStatusHistory => Set<CookAtHomeOrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.Email).IsUnique(); e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<RefreshToken>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CookProfile>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.BusinessName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000); e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.CookProfile).HasForeignKey<CookProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Food>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Cuisine).HasMaxLength(80).IsRequired(); e.Property(x => x.Tags).HasMaxLength(500); e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Price).HasPrecision(18, 2); e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.HasOne(x => x.CookProfile).WithMany(x => x.Foods).HasForeignKey(x => x.CookProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Order>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired(); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2); e.Property(x => x.FulfilmentType).HasMaxLength(30);
            e.Property(x => x.DeliveryAddress).HasMaxLength(1000); e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CookProfile).WithMany().HasForeignKey(x => x.CookProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrderItem>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.FoodName).HasMaxLength(160).IsRequired(); e.Property(x => x.UnitPrice).HasPrecision(18,2);
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CookAtHomeOrder>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            e.Property(x => x.Location).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ContactName).HasMaxLength(120).IsRequired();
            e.Property(x => x.ContactPhone).HasMaxLength(50).IsRequired();
            e.Property(x => x.SpecialInstructions).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.EstimatedPrice).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CookProfile).WithMany().HasForeignKey(x => x.CookProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CookAtHomeOrderStatusHistory>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.CookAtHomeOrderId, x.CreatedAtUtc });
            e.HasOne(x => x.CookAtHomeOrder).WithMany(x => x.StatusHistory).HasForeignKey(x => x.CookAtHomeOrderId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OutboxMessage>(e => {
            e.HasKey(x => x.Id); e.Property(x => x.Type).HasMaxLength(200).IsRequired(); e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(2000); e.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
        });
    }
}
