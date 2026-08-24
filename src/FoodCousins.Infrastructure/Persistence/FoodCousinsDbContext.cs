using FoodCousins.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Persistence;

public sealed class FoodCousinsDbContext(DbContextOptions<FoodCousinsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<FoodIngredient> FoodIngredients => Set<FoodIngredient>();
    public DbSet<FoodSimilarity> FoodSimilarities => Set<FoodSimilarity>();
    public DbSet<AiFoodRequest> AiFoodRequests => Set<AiFoodRequest>();
    public DbSet<DeliveryArea> DeliveryAreas => Set<DeliveryArea>();
    public DbSet<CookAtHomeOrder> CookAtHomeOrders => Set<CookAtHomeOrder>();
    public DbSet<CookAtHomeOrderIngredient> CookAtHomeOrderIngredients => Set<CookAtHomeOrderIngredient>();
    public DbSet<CookAtHomeOrderStatusHistory> CookAtHomeOrderStatusHistory => Set<CookAtHomeOrderStatusHistory>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Food>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2500);
            e.Property(x => x.Cuisine).HasMaxLength(100).IsRequired();
            e.Property(x => x.CountryOrRegion).HasMaxLength(120);
            e.Property(x => x.Tags).HasMaxLength(1000);
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.KitPricePerPerson).HasPrecision(18, 2);
            e.HasIndex(x => x.NormalizedName).IsUnique();
        });

        b.Entity<Recipe>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.FoodId).IsUnique();
            e.HasOne(x => x.Food).WithOne(x => x.Recipe)
                .HasForeignKey<Recipe>(x => x.FoodId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecipeStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Instruction).HasMaxLength(3000).IsRequired();
            e.HasIndex(x => new { x.RecipeId, x.StepNumber }).IsUnique();
            e.HasOne(x => x.Recipe).WithMany(x => x.Steps)
                .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Ingredient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(160).IsRequired();
            e.HasIndex(x => x.NormalizedName).IsUnique();
        });

        b.Entity<FoodIngredient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QuantityPerServing).HasPrecision(18, 4);
            e.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.FoodId, x.IngredientId }).IsUnique();
            e.HasOne(x => x.Food).WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient).WithMany(x => x.Foods)
                .HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FoodSimilarity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OverallScore).HasPrecision(5, 2);
            e.Property(x => x.TasteScore).HasPrecision(5, 2);
            e.Property(x => x.TextureScore).HasPrecision(5, 2);
            e.Property(x => x.IngredientScore).HasPrecision(5, 2);
            e.Property(x => x.CookingMethodScore).HasPrecision(5, 2);
            e.Property(x => x.DishTypeScore).HasPrecision(5, 2);
            e.Property(x => x.CuisineScore).HasPrecision(5, 2);
            e.Property(x => x.DietaryScore).HasPrecision(5, 2);
            e.Property(x => x.WhySimilar).HasMaxLength(1500).IsRequired();
            e.HasIndex(x => new { x.SourceFoodId, x.RelatedFoodId }).IsUnique();
            e.HasOne(x => x.SourceFood).WithMany(x => x.Similarities)
                .HasForeignKey(x => x.SourceFoodId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RelatedFood).WithMany()
                .HasForeignKey(x => x.RelatedFoodId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AiFoodRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Query).HasMaxLength(200).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            e.Property(x => x.Model).HasMaxLength(100).IsRequired();
            e.Property(x => x.FailureMessage).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.Query, x.Succeeded, x.CompletedAtUtc });
        });

        b.Entity<DeliveryArea>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Postcode).HasMaxLength(12).IsRequired();
            e.Property(x => x.Suburb).HasMaxLength(120).IsRequired();
            e.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            e.Property(x => x.MinimumOrderAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.Postcode).IsUnique();
        });

        b.Entity<CookAtHomeOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            e.Property(x => x.Postcode).HasMaxLength(12).IsRequired();
            e.Property(x => x.Suburb).HasMaxLength(120).IsRequired();
            e.Property(x => x.DeliveryAddress).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ContactName).HasMaxLength(120).IsRequired();
            e.Property(x => x.ContactPhone).HasMaxLength(50).IsRequired();
            e.Property(x => x.SpecialInstructions).HasMaxLength(2000);
            e.Property(x => x.RecipeTitle).HasMaxLength(200).IsRequired();
            e.Property(x => x.RecipeInstructions).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CookAtHomeOrderIngredient>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.IngredientName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.CookAtHomeOrder).WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.CookAtHomeOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CookAtHomeOrderStatusHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => new { x.CookAtHomeOrderId, x.CreatedAtUtc });
            e.HasOne(x => x.CookAtHomeOrder).WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.CookAtHomeOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(200).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
        });
    }
}
