using FoodBot.Data;

namespace FoodBot.Services;

public sealed class MealRepository : IMealRepository
{
    private readonly BotDbContext _db;

    public MealRepository(BotDbContext db)
    {
        _db = db;
    }

    public IQueryable<MealEntry> Meals => _db.Meals;
    public IQueryable<PendingMeal> PendingMeals => _db.PendingMeals;
    public IQueryable<PendingClarify> PendingClarifies => _db.PendingClarifies;

    public Task QueuePendingMealAsync(PendingMeal meal, CancellationToken ct)
    {
        _db.PendingMeals.Add(meal);
        return _db.SaveChangesAsync(ct);
    }

    public Task QueuePendingClarifyAsync(PendingClarify clarify, CancellationToken ct)
    {
        _db.PendingClarifies.Add(clarify);
        return _db.SaveChangesAsync(ct);
    }

    public Task RemoveMealAsync(MealEntry meal, CancellationToken ct)
    {
        _db.Meals.Remove(meal);
        return _db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
