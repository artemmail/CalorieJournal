using FoodBot.Data;

namespace FoodBot.Services;

public interface IMealRepository
{
    IQueryable<MealEntry> Meals { get; }
    IQueryable<PendingMeal> PendingMeals { get; }
    IQueryable<PendingClarify> PendingClarifies { get; }

    Task QueuePendingMealAsync(PendingMeal meal, CancellationToken ct);
    Task QueuePendingClarifyAsync(PendingClarify clarify, CancellationToken ct);
    Task RemoveMealAsync(MealEntry meal, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
