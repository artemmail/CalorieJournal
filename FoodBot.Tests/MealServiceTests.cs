using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Xunit;

public class MealServiceTests
{
    private MealService CreateService(List<MealEntry> meals)
    {
        var repo = new FakeRepo(meals);
        return new MealService(repo);
    }

    private sealed class FakeRepo : IMealRepository
    {
        public List<MealEntry> MealsData;
        public List<PendingMeal> PendingMealData = new();
        public List<PendingClarify> PendingClarifyData = new();
        public FakeRepo(List<MealEntry> meals) { MealsData = meals; }
        public IQueryable<MealEntry> Meals => MealsData.AsQueryable();
        public IQueryable<PendingMeal> PendingMeals => PendingMealData.AsQueryable();
        public IQueryable<PendingClarify> PendingClarifies => PendingClarifyData.AsQueryable();
        public Task QueuePendingMealAsync(PendingMeal meal, CancellationToken ct)
        { PendingMealData.Add(meal); return Task.CompletedTask; }
        public Task QueuePendingClarifyAsync(PendingClarify clarify, CancellationToken ct)
        { PendingClarifyData.Add(clarify); return Task.CompletedTask; }
        public Task RemoveMealAsync(MealEntry meal, CancellationToken ct)
        { MealsData.Remove(meal); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task ListAsync_ReturnsMealsForChat()
    {
        var meals = new List<MealEntry>
        {
            new MealEntry { Id = 1, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow },
            new MealEntry { Id = 2, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) },
            new MealEntry { Id = 3, ChatId = 2, CreatedAtUtc = DateTimeOffset.UtcNow }
        };
        var service = CreateService(meals);
        var res = await service.ListAsync(1, 10, 0, CancellationToken.None);
        Assert.Equal(2, res.Total);
        Assert.Equal(2, res.Items.Count);
    }

    [Fact]
    public async Task DeleteAsync_RemovesMeal()
    {
        var meals = new List<MealEntry>
        {
            new MealEntry { Id = 1, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow }
        };
        var service = CreateService(meals);
        var ok = await service.DeleteAsync(1, 1, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(meals);
        ok = await service.DeleteAsync(1, 1, CancellationToken.None);
        Assert.False(ok);
    }
}
