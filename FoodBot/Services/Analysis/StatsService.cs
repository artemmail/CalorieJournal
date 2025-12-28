using System;
using System.Collections.Generic;
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
        var to = DateTimeOffset.UtcNow.Date.AddDays(1);

        var totals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.AppUserId == chatId && m.CreatedAtUtc >= from && m.CreatedAtUtc < to)
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

    public async Task<List<DailyTotals>> GetDailyTotalsAsync(long chatId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc).AddDays(1);

        var results = await _db.Meals
            .AsNoTracking()
            .Where(m => m.AppUserId == chatId && m.CreatedAtUtc >= start && m.CreatedAtUtc < end)
            .GroupBy(m => m.CreatedAtUtc.Date)
            .Select(g => new DailyTotals
            {
                Date = g.Key,
                Totals = new MacroTotals
                {
                    Calories = g.Sum(m => m.CaloriesKcal) ?? 0,
                    Proteins = g.Sum(m => m.ProteinsG) ?? 0,
                    Fats = g.Sum(m => m.FatsG) ?? 0,
                    Carbs = g.Sum(m => m.CarbsG) ?? 0,
                }
            })
            .ToListAsync(ct);

        var list = new List<DailyTotals>();
        for (var day = start; day < end; day = day.AddDays(1))
        {
            var item = results.FirstOrDefault(r => r.Date == day);
            list.Add(item ?? new DailyTotals { Date = day, Totals = new MacroTotals() });
        }
        // remove empty days until the first one containing a meal
        // but keep at least one day to show zero totals for the current day
        while (list.Count > 1 &&
               list[0].Totals.Calories == 0 &&
               list[0].Totals.Proteins == 0 &&
               list[0].Totals.Fats == 0 &&
               list[0].Totals.Carbs == 0)
        {
            list.RemoveAt(0);
        }
        return list;
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

public sealed class DailyTotals
{
    public DateTime Date { get; set; }
    public required MacroTotals Totals { get; set; }
}

