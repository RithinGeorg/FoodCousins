using FoodCousins.Application.Foods;
using FoodCousins.Domain.Entities;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Infrastructure.Foods;

internal sealed class FoodService(
    FoodCousinsDbContext db,
    IFoodDiscoveryProvider discoveryProvider,
    ILogger<FoodService> logger) : IFoodService
{
    public async Task<IReadOnlyList<FoodSummaryDto>> GetPublishedAsync(CancellationToken ct)
    {
        var foods = await db.Foods.AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.Name)
            .Take(100)
            .ToListAsync(ct);

        return foods.Select(MapSummary).ToList();
    }

    public async Task<FoodSearchResultDto> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Food search query is required.");
        var trimmedQuery = query.Trim();
        var normalized = NormalizeFoodName(trimmedQuery);
        if (normalized.Length < 2) throw new ArgumentException("Enter at least two characters to search for a food.");

        var existing = await db.Foods.AsNoTracking()
            .SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);

        if (existing is not null)
        {
            if (!existing.IsPublished) throw new KeyNotFoundException("This food is not currently published.");

            logger.LogInformation("Food search served from database. Query={Query}, FoodId={FoodId}.", query, existing.Id);
            return new FoodSearchResultDto(
                trimmedQuery,
                "Database",
                MapSummary(existing),
                await GetCousinsAsync(existing.Id, ct));
        }

        // A previous AI request may have resolved a non-canonical phrase (for example
        // "chicken butter") to a canonical food name ("Butter Chicken"). Reuse that
        // resolution before spending on another external call.
        var priorFoodId = await db.AiFoodRequests.AsNoTracking()
            .Where(x => x.Succeeded && x.FoodId != null && x.Query == trimmedQuery)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => x.FoodId)
            .FirstOrDefaultAsync(ct);

        if (priorFoodId is Guid cachedFoodId)
        {
            // Resolve the cached food regardless of publication state. If an admin has hidden
            // the food, do not spend money calling AI again for the same phrase.
            var cached = await db.Foods.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == cachedFoodId, ct);

            if (cached is not null)
            {
                if (!cached.IsPublished)
                    throw new KeyNotFoundException("This food is not currently published.");

                logger.LogInformation("Food search served from previous AI query cache. Query={Query}, FoodId={FoodId}.", query, cached.Id);
                return new FoodSearchResultDto(
                    trimmedQuery,
                    "Database",
                    MapSummary(cached),
                    await GetCousinsAsync(cached.Id, ct));
            }
        }

        var audit = new AiFoodRequest
        {
            Query = trimmedQuery,
            Provider = discoveryProvider.ProviderName,
            Model = discoveryProvider.ModelName
        };
        db.AiFoodRequests.Add(audit);
        await db.SaveChangesAsync(ct);

        AiFoodDiscoveryContract discovered;
        try
        {
            discovered = await discoveryProvider.DiscoverAsync(trimmedQuery, ct);
        }
        catch (Exception ex)
        {
            audit.Succeeded = false;
            audit.CompletedAtUtc = DateTimeOffset.UtcNow;
            audit.FailureMessage = ex.Message[..Math.Min(ex.Message.Length, 2000)];
            await db.SaveChangesAsync(ct);
            throw;
        }

        try
        {
            await using (var transaction =
                await db.Database.BeginTransactionAsync(ct))
            {
                try
                {
                    var primary =
                        await GetOrCreateFoodAsync(discovered.Food, ct);

                    var relatedFoodIds = new HashSet<Guid>();

                    foreach (var cousinContract in discovered.Cousins)
                    {
                        var cousin =
                            await GetOrCreateFoodAsync(
                                cousinContract.Food,
                                ct);

                        if (cousin.Id == primary.Id ||
                            !relatedFoodIds.Add(cousin.Id))
                        {
                            continue;
                        }

                        var existingSimilarity =
                            await db.FoodSimilarities
                                .SingleOrDefaultAsync(
                                    x =>
                                        x.SourceFoodId == primary.Id &&
                                        x.RelatedFoodId == cousin.Id,
                                    ct);

                        if (existingSimilarity is null)
                        {
                            db.FoodSimilarities.Add(
                                MapSimilarity(
                                    primary.Id,
                                    cousin.Id,
                                    cousinContract.Similarity));
                        }

                        var existingReverse =
                            await db.FoodSimilarities
                                .SingleOrDefaultAsync(
                                    x =>
                                        x.SourceFoodId == cousin.Id &&
                                        x.RelatedFoodId == primary.Id,
                                    ct);

                        if (existingReverse is null)
                        {
                            db.FoodSimilarities.Add(
                                MapSimilarity(
                                    cousin.Id,
                                    primary.Id,
                                    cousinContract.Similarity));
                        }
                    }

                    audit.Succeeded = true;
                    audit.FoodId = primary.Id;
                    audit.CompletedAtUtc = DateTimeOffset.UtcNow;
                    audit.FailureMessage = null;

                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);

                    logger.LogInformation(
                        "Food discovery saved AI result to database. " +
                        "Query={Query}, FoodId={FoodId}, CousinCount={CousinCount}.",
                        query,
                        primary.Id,
                        discovered.Cousins.Count);

                    return new FoodSearchResultDto(
                        trimmedQuery,
                        "AI",
                        MapSummary(primary),
                        await GetCousinsAsync(primary.Id, ct));
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }
        }
        catch (DbUpdateException ex)
        {
            // At this point the failed transaction has already been
            // rolled back AND disposed.
            db.ChangeTracker.Clear();

            // Did another request create the same canonical food first?
            var winner = await db.Foods
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.NormalizedName == normalized,
                    ct);

            // No winner means this DbUpdateException was caused by
            // something else. Preserve the original database error.
            if (winner is null)
            {
                throw;
            }

            logger.LogWarning(
                ex,
                "Concurrent food discovery detected. Another request " +
                "created the food first. Query={Query}, FoodId={FoodId}.",
                query,
                winner.Id);

            // Our AI request itself succeeded, so complete its audit record.
            var persistedAudit = await db.AiFoodRequests
                .SingleAsync(
                    x => x.Id == audit.Id,
                    ct);

            persistedAudit.Succeeded = true;
            persistedAudit.FoodId = winner.Id;
            persistedAudit.CompletedAtUtc = DateTimeOffset.UtcNow;
            persistedAudit.FailureMessage = null;

            await db.SaveChangesAsync(ct);

            return new FoodSearchResultDto(
                trimmedQuery,
                "Database",
                MapSummary(winner),
                await GetCousinsAsync(winner.Id, ct));
        }
    }

    public async Task<FoodDetailDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var food = await db.Foods.AsNoTracking()
            .Include(x => x.Recipe).ThenInclude(x => x!.Steps)
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .SingleOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);

        if (food is null) return null;
        if (food.Recipe is null) throw new InvalidOperationException("Food recipe is missing.");

        return new FoodDetailDto(
            MapSummary(food),
            new RecipeDto(
                food.Recipe.Title,
                food.Recipe.PrepMinutes,
                food.Recipe.CookMinutes,
                food.Recipe.Servings,
                food.Recipe.Steps.OrderBy(x => x.StepNumber)
                    .Select(x => new RecipeStepDto(x.StepNumber, x.Instruction)).ToList()),
            food.Ingredients.OrderBy(x => x.Ingredient.Name)
                .Select(x => new FoodIngredientDto(
                    x.Ingredient.Name,
                    x.QuantityPerServing,
                    x.Unit,
                    x.IsOptional,
                    x.Notes))
                .ToList(),
            await GetCousinsAsync(food.Id, ct));
    }

    public async Task<IReadOnlyList<FoodCousinDto>> GetCousinsAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.FoodSimilarities.AsNoTracking()
            .Include(x => x.RelatedFood)
            .Where(x => x.SourceFoodId == id && x.RelatedFood.IsPublished)
            .OrderByDescending(x => x.OverallScore)
            .Take(12)
            .ToListAsync(ct);

        return rows.Select(x => new FoodCousinDto(
            MapSummary(x.RelatedFood),
            new SimilarityBreakdownDto(
                x.OverallScore,
                x.TasteScore,
                x.TextureScore,
                x.IngredientScore,
                x.CookingMethodScore,
                x.DishTypeScore,
                x.CuisineScore,
                x.DietaryScore,
                x.WhySimilar)))
            .ToList();
    }

    private async Task<Food> GetOrCreateFoodAsync(AiFoodContract contract, CancellationToken ct)
    {
        var normalized = NormalizeFoodName(contract.Name);

        var existing = await db.Foods
            .Include(x => x.Recipe).ThenInclude(x => x!.Steps)
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);

        if (existing is not null) return existing;

        var food = new Food
        {
            Name = Clean(contract.Name, 160),
            NormalizedName = normalized,
            Description = CleanNullable(contract.Description, 2500),
            Cuisine = Clean(contract.Cuisine, 100),
            CountryOrRegion = CleanNullable(contract.CountryOrRegion, 120),
            Tags = CleanNullable(contract.Tags, 1000),
            KitPricePerPerson = 0m,
            IsCookAtHomeEnabled = false,
            IsPublished = true,
            AiGenerated = true,
            AiReviewed = false
        };

        food.Recipe = new Recipe
        {
            FoodId = food.Id,
            Title = Clean(contract.Recipe.Title, 200),
            PrepMinutes = Math.Clamp(contract.Recipe.PrepMinutes, 0, 1440),
            CookMinutes = Math.Clamp(contract.Recipe.CookMinutes, 0, 1440),
            Servings = Math.Clamp(contract.Recipe.Servings, 1, 100),
            Steps = contract.Recipe.Steps
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(30)
                .Select((x, index) => new RecipeStep
                {
                    StepNumber = index + 1,
                    Instruction = Clean(x, 3000)
                })
                .ToList()
        };

        foreach (var ingredientContract in contract.Ingredients.Take(100))
        {
            if (string.IsNullOrWhiteSpace(ingredientContract.Name)) continue;

            var ingredientName = Clean(ingredientContract.Name, 160);
            var ingredientNormalized = NormalizeIngredientName(ingredientName);

            var ingredient = db.Ingredients.Local.FirstOrDefault(x => x.NormalizedName == ingredientNormalized)
                ?? await db.Ingredients.SingleOrDefaultAsync(x => x.NormalizedName == ingredientNormalized, ct);

            if (ingredient is null)
            {
                ingredient = new Ingredient
                {
                    Name = ingredientName,
                    NormalizedName = ingredientNormalized
                };
                db.Ingredients.Add(ingredient);
            }

            food.Ingredients.Add(new FoodIngredient
            {
                FoodId = food.Id,
                Ingredient = ingredient,
                IngredientId = ingredient.Id,
                QuantityPerServing = Math.Max(0m, ingredientContract.QuantityPerServing),
                Unit = Clean(ingredientContract.Unit, 40),
                IsOptional = ingredientContract.IsOptional,
                Notes = CleanNullable(ingredientContract.Notes, 500)
            });
        }

        db.Foods.Add(food);
        await db.SaveChangesAsync(ct);
        return food;
    }

    private static FoodSimilarity MapSimilarity(Guid sourceFoodId, Guid relatedFoodId, AiSimilarityContract x) => new()
    {
        SourceFoodId = sourceFoodId,
        RelatedFoodId = relatedFoodId,
        OverallScore = ClampScore(x.Overall),
        TasteScore = ClampScore(x.Taste),
        TextureScore = ClampScore(x.Texture),
        IngredientScore = ClampScore(x.Ingredients),
        CookingMethodScore = ClampScore(x.CookingMethod),
        DishTypeScore = ClampScore(x.DishType),
        CuisineScore = ClampScore(x.Cuisine),
        DietaryScore = ClampScore(x.Dietary),
        WhySimilar = Clean(x.WhySimilar, 1500)
    };

    internal static FoodSummaryDto MapSummary(Food food) => new(
        food.Id,
        food.Name,
        food.Description,
        food.Cuisine,
        food.CountryOrRegion,
        food.Tags,
        food.KitPricePerPerson,
        food.IsPublished,
        food.IsCookAtHomeEnabled,
        food.ImageUrl,
        food.AiGenerated,
        food.AiReviewed);

    internal static string NormalizeFoodName(string value) => Normalize(value, 160);
    private static string NormalizeIngredientName(string value) => Normalize(value, 160);

    private static string Normalize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.");
        var cleaned = string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length > maxLength) cleaned = cleaned[..maxLength];
        return cleaned.ToUpperInvariant();
    }

    private static string Clean(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("AI food data contains an empty required value.");
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string? CleanNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static decimal ClampScore(decimal score) => Math.Clamp(score, 0m, 100m);
}
