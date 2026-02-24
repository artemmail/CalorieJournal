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
    private static TimeZoneInfo MoscowTz => GetMoscowTz();

    public StatsService(BotDbContext db)
    {
        _db = db;
    }

    public async Task<StatsSummary> GetSummaryAsync(long chatId, int days, CancellationToken ct = default)
    {
        if (days <= 0) days = 1;
        var tz = MoscowTz;
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var fromLocal = nowLocal.Date.AddDays(-(days - 1));
        var toLocalExclusive = nowLocal.Date.AddDays(1);

        var fromUtc = new DateTimeOffset(fromLocal, tz.GetUtcOffset(fromLocal)).UtcDateTime;
        var toUtc = new DateTimeOffset(toLocalExclusive, tz.GetUtcOffset(toLocalExclusive)).UtcDateTime;

        var totals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtc)
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
        var tz = MoscowTz;
        var startLocal = from.Date;
        var endLocalExclusive = to.Date.AddDays(1);
        if (endLocalExclusive <= startLocal)
            return new List<DailyTotals>();

        var startUtc = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal)).UtcDateTime;
        var endUtc = new DateTimeOffset(endLocalExclusive, tz.GetUtcOffset(endLocalExclusive)).UtcDateTime;

        var meals = await _db.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= startUtc && m.CreatedAtUtc < endUtc)
            .Select(m => new
            {
                m.CreatedAtUtc,
                Calories = m.CaloriesKcal ?? 0,
                Proteins = m.ProteinsG ?? 0,
                Fats = m.FatsG ?? 0,
                Carbs = m.CarbsG ?? 0
            })
            .ToListAsync(ct);

        var results = meals
            .GroupBy(m => TimeZoneInfo.ConvertTime(m.CreatedAtUtc, tz).Date)
            .ToDictionary(
                g => g.Key,
                g => new MacroTotals
                {
                    Calories = g.Sum(x => x.Calories),
                    Proteins = g.Sum(x => x.Proteins),
                    Fats = g.Sum(x => x.Fats),
                    Carbs = g.Sum(x => x.Carbs)
                });

        var list = new List<DailyTotals>();
        for (var day = startLocal; day < endLocalExclusive; day = day.AddDays(1))
        {
            list.Add(new DailyTotals
            {
                Date = day,
                Totals = results.TryGetValue(day, out var totals) ? totals : new MacroTotals()
            });
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

    private static TimeZoneInfo GetMoscowTz()
    {
        string[] ids = { "Europe/Moscow", "Russian Standard Time" };
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "UTC+03", "UTC+03");
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

