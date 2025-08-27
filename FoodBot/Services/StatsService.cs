using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FoodBot.Data;

namespace FoodBot.Services;

public sealed class StatsService
{
    private readonly BotDbContext _db;

    public StatsService(BotDbContext db)
    {
        _db = db;
    }

    public async Task<StatsSummary> GetSummaryAsync(long chatId, int days, CancellationToken ct = default)
    {
        if (days <= 0) days = 1;
        var from = DateTimeOffset.UtcNow.Date.AddDays(-(days - 1));
        var to = DateTimeOffset.UtcNow;

        var totals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= from && m.CreatedAtUtc <= to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Calories = g.Sum(m => m.CaloriesKcal) ?? 0,
                Proteins = g.Sum(m => m.ProteinsG) ?? 0,
                Fats = g.Sum(m => m.FatsG) ?? 0,
                Carbs = g.Sum(m => m.CarbsG) ?? 0,
                Count = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        if (totals == null)
        {
            return new StatsSummary
            {
                Totals = new MacroTotals(),
                Days = days,
                Entries = 0
            };
        }

        return new StatsSummary
        {
            Totals = new MacroTotals
            {
                Calories = totals.Calories,
                Proteins = totals.Proteins,
                Fats = totals.Fats,
                Carbs = totals.Carbs
            },
            Days = days,
            Entries = totals.Count
        };
    }
}

public sealed class StatsSummary
{
    public required MacroTotals Totals { get; set; }
    public int Days { get; set; }
    public int Entries { get; set; }
}

public sealed class MacroTotals
{
    public decimal Calories { get; set; }
    public decimal Proteins { get; set; }
    public decimal Fats { get; set; }
    public decimal Carbs { get; set; }
}

