using FoodCousins.Application.Foods;
using FoodCousins.Application.Storage;
using FoodCousins.Domain.Entities;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Infrastructure.Foods;
internal sealed class FoodService(FoodCousinsDbContext db, IImageStorage images) : IFoodService
{
    public async Task<IReadOnlyList<FoodDto>> GetAvailableAsync(CancellationToken ct) => await SearchAsync(null, null, ct);

    public async Task<IReadOnlyList<FoodDto>> SearchAsync(string? query, string? cuisine, CancellationToken ct)
    {
        var q = db.Foods.AsNoTracking().Where(x => x.IsAvailable && x.CookProfile.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var value = query.Trim();
            q = q.Where(x => x.Name.Contains(value) || (x.Description != null && x.Description.Contains(value)) || (x.Tags != null && x.Tags.Contains(value)));
        }
        if (!string.IsNullOrWhiteSpace(cuisine)) { var c = cuisine.Trim(); q = q.Where(x => x.Cuisine == c); }
        return await q.OrderBy(x => x.Name).Take(100).Select(Map()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FoodCousinDto>> GetCousinsAsync(Guid id, CancellationToken ct)
    {
        var source = await db.Foods.AsNoTracking().Include(x => x.CookProfile).SingleOrDefaultAsync(x => x.Id == id && x.IsAvailable, ct)
            ?? throw new KeyNotFoundException("Food not found.");
        var candidates = await db.Foods.AsNoTracking().Where(x => x.Id != id && x.IsAvailable && x.CookProfile.IsActive).Select(Map()).ToListAsync(ct);
        var sourceTags = Tokens(source.Tags, source.Name, source.Description);
        return candidates.Select(food =>
        {
            var tags = Tokens(food.Tags, food.Name, food.Description);
            var intersection = sourceTags.Intersect(tags).Count();
            var union = Math.Max(1, sourceTags.Union(tags).Count());
            var tagScore = (double)intersection / union;
            var sameCuisine = string.Equals(source.Cuisine, food.Cuisine, StringComparison.OrdinalIgnoreCase);
            var score = Math.Min(0.99, 0.35 + tagScore * 0.5 + (sameCuisine ? 0.14 : 0));
            var why = intersection > 0 ? $"Shares {intersection} flavour/food tag{(intersection == 1 ? "" : "s")}{(sameCuisine ? " and the same cuisine" : " across cuisines")}." : sameCuisine ? "A different dish from the same cuisine." : "A cross-culture cousin to explore.";
            return new FoodCousinDto(food, Math.Round(score, 2), why);
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Food.Name).Take(8).ToList();
    }

    public Task<FoodDto?> GetByIdAsync(Guid id,CancellationToken ct)=>db.Foods.AsNoTracking().Where(x=>x.Id==id).Select(Map()).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<FoodDto>> GetCookFoodsAsync(Guid userId,CancellationToken ct)=>await db.Foods.AsNoTracking().Where(x=>x.CookProfile.UserId==userId).OrderByDescending(x=>x.CreatedAtUtc).Select(Map()).ToListAsync(ct);
    public async Task<FoodDto> CreateAsync(Guid userId,UpsertFoodRequest r,CancellationToken ct)
    {
        Validate(r); var cook=await db.CookProfiles.SingleOrDefaultAsync(x=>x.UserId==userId,ct)??throw new UnauthorizedAccessException("Cook profile not found.");
        var f=new Food{CookProfileId=cook.Id,Name=r.Name.Trim(),Description=r.Description?.Trim(),Cuisine=r.Cuisine.Trim(),Tags=NormalizeTags(r.Tags),Price=r.Price,IsAvailable=r.IsAvailable};
        db.Foods.Add(f); await db.SaveChangesAsync(ct); return (await GetByIdAsync(f.Id,ct))!;
    }
    public async Task<FoodDto> UpdateAsync(Guid userId,Guid id,UpsertFoodRequest r,CancellationToken ct)
    {
        Validate(r); var f=await db.Foods.Include(x=>x.CookProfile).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Food not found.");
        if(f.CookProfile.UserId!=userId) throw new UnauthorizedAccessException("You do not own this food.");
        f.Name=r.Name.Trim();f.Description=r.Description?.Trim();f.Cuisine=r.Cuisine.Trim();f.Tags=NormalizeTags(r.Tags);f.Price=r.Price;f.IsAvailable=r.IsAvailable;
        await db.SaveChangesAsync(ct); return (await GetByIdAsync(f.Id,ct))!;
    }
    public async Task<FoodDto> SetImageAsync(Guid userId,Guid id,Stream content,string contentType,string fileName,CancellationToken ct)
    {
        var f=await db.Foods.Include(x=>x.CookProfile).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("Food not found.");
        if(f.CookProfile.UserId!=userId) throw new UnauthorizedAccessException("You do not own this food.");
        f.ImageUrl=await images.UploadFoodImageAsync(id,content,contentType,fileName,ct); await db.SaveChangesAsync(ct); return (await GetByIdAsync(id,ct))!;
    }
    private static void Validate(UpsertFoodRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 160) throw new ArgumentException("Food name is required and must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(r.Cuisine) || r.Cuisine.Trim().Length > 80) throw new ArgumentException("Cuisine is required and must be 80 characters or fewer.");
        if (r.Description?.Trim().Length > 2000) throw new ArgumentException("Description must be 2000 characters or fewer.");
        if (r.Price <= 0 || r.Price > 100_000) throw new ArgumentException("Price must be greater than zero and no more than 100000.");
    }
    private static string? NormalizeTags(string? tags) => string.IsNullOrWhiteSpace(tags) ? null : string.Join(',', tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).Distinct().Take(20));
    private static HashSet<string> Tokens(params string?[] values) => values.Where(x => !string.IsNullOrWhiteSpace(x)).SelectMany(x => x!.ToLowerInvariant().Split(new[]{' ', ',', '-', '/', '.'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Where(x => x.Length > 2).ToHashSet();
    private static System.Linq.Expressions.Expression<Func<Food,FoodDto>> Map()=>x=>new FoodDto(x.Id,x.CookProfileId,x.CookProfile.BusinessName,x.Name,x.Description,x.Cuisine,x.Tags,x.Price,x.IsAvailable,x.ImageUrl);
}
