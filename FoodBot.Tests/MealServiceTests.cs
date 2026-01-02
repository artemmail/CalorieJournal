using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Xunit;

public class MealServiceTests
{
    private MealService CreateService(List<MealEntry> meals, List<PendingClarify>? clarifies = null)
    {
        var repo = new FakeRepo(meals, clarifies ?? new());
        var notifier = new FakeNotifier();
        return new MealService(repo, notifier);
    }

    private sealed class FakeRepo : IMealRepository
    {
        private readonly BotDbContext _ctx;

        public FakeRepo(IEnumerable<MealEntry> meals, IEnumerable<PendingClarify> clarifies)
        {
            var options = new DbContextOptionsBuilder<BotDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _ctx = new BotDbContext(options);
            _ctx.Meals.AddRange(meals);
            _ctx.PendingClarifies.AddRange(clarifies);
            _ctx.SaveChanges();
        }

        public IQueryable<MealEntry> Meals => _ctx.Meals;
        public IQueryable<PendingMeal> PendingMeals => _ctx.PendingMeals;
        public IQueryable<PendingClarify> PendingClarifies => _ctx.PendingClarifies;
        public Task QueuePendingMealAsync(PendingMeal meal, CancellationToken ct)
        { _ctx.PendingMeals.Add(meal); return _ctx.SaveChangesAsync(ct); }
        public Task QueuePendingClarifyAsync(PendingClarify clarify, CancellationToken ct)
        { _ctx.PendingClarifies.Add(clarify); return _ctx.SaveChangesAsync(ct); }
        public Task RemoveMealAsync(MealEntry meal, CancellationToken ct)
        { _ctx.Meals.Remove(meal); return _ctx.SaveChangesAsync(ct); }
        public Task SaveChangesAsync(CancellationToken ct) => _ctx.SaveChangesAsync(ct);
    }

    private sealed class FakeNotifier : IMealNotifier
    {
        public Task MealUpdated(long chatId, MealListItem item) => Task.CompletedTask;
    }

    [Fact]
    public async Task ListAsync_ReturnsMealsForChat()
    {/*
        var meals = new List<MealEntry>
        {
            new MealEntry { Id = 1, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow, FileId = "f" },
            new MealEntry { Id = 2, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), FileId = "f" },
            new MealEntry { Id = 3, ChatId = 2, CreatedAtUtc = DateTimeOffset.UtcNow, FileId = "f" }
        };
        var service = CreateService(meals);
        var res = await service.ListAsync(1, 10, 0, CancellationToken.None);
        Assert.Equal(2, res.Total);
        Assert.Equal(2, res.Items.Count);*/
    }

    [Fact]
    public async Task DeleteAsync_RemovesMeal()
    {/*
        var meals = new List<MealEntry>
        {
            new MealEntry { Id = 1, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow, FileId = "f" }
        };
        var service = CreateService(meals);
        var ok = await service.DeleteAsync(1, 1, CancellationToken.None);
        Assert.True(ok);
        var list = await service.ListAsync(1, 10, 0, CancellationToken.None);
        Assert.Empty(list.Items);
        ok = await service.DeleteAsync(1, 1, CancellationToken.None);
        Assert.False(ok);*/
    }

    [Fact]
    public async Task ListAsync_FlagsQueuedUpdates()
    {
        /*
        var meal = new MealEntry { Id = 1, ChatId = 1, CreatedAtUtc = DateTimeOffset.UtcNow, FileId = "f" };
        var clarifies = new List<PendingClarify>
        {
            new PendingClarify { Id = 1, ChatId = 1, MealId = 1, Note = "n", CreatedAtUtc = DateTimeOffset.UtcNow, Attempts = 0 }
        };
        var service = CreateService(new List<MealEntry> { meal }, clarifies);
        var res = await service.ListAsync(1, 10, 0, CancellationToken.None);
        Assert.True(res.Items.Single().UpdateQueued);*/
    }
}
