using System.Globalization;
using System.Text.Json;
using FoodBot.Data;
using FoodBot.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodBot.Services.Reports;

public sealed class ReportDataLoader : IReportDataLoader
{
    private readonly BotDbContext _db;
    public ReportDataLoader(BotDbContext db) => _db = db;

    private static TimeZoneInfo MoscowTz => GetMoscowTz();

    public async Task<ReportData<ReportPayload>> LoadAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        var tz = MoscowTz;
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);
        var (periodStartUtc, periodHuman, _, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);

        var card = await _db.PersonalCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

        int? age = null;
        if (card?.BirthYear is int by && by > 1900 && by <= nowLocal.Year)
            age = nowLocal.Year - by;

        var mealsRaw = await _db.Meals.AsNoTracking()
            .Where(m => m.ChatId == chatId &&
                        m.CreatedAtUtc >= periodStartUtc.UtcDateTime &&
                        m.CreatedAtUtc <= nowUtc.UtcDateTime)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new
            {
                m.DishName,
                m.CreatedAtUtc,
                m.CaloriesKcal,
                m.ProteinsG,
                m.FatsG,
                m.CarbsG
            })
            .ToListAsync(ct);

        var meals = mealsRaw.Select(m =>
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(m.CreatedAtUtc.UtcDateTime, tz);
            return new MealEntry
            {
                Dish = m.DishName ?? string.Empty,
                LocalDate = local.ToString("yyyy-MM-dd"),
                LocalTime = local.ToString("HH:mm"),
                LocalDateTimeIso = local.ToString("yyyy-MM-dd HH:mm"),
                Calories = m.CaloriesKcal ?? 0,
                Proteins = m.ProteinsG ?? 0,
                Fats = m.FatsG ?? 0,
                Carbs = m.CarbsG ?? 0
            };
        }).ToList();

        var totals = new Totals
        {
            Calories = meals.Sum(x => x.Calories),
            Proteins = meals.Sum(x => x.Proteins),
            Fats = meals.Sum(x => x.Fats),
            Carbs = meals.Sum(x => x.Carbs),
            MealsCount = meals.Count
        };

        List<string>? hourGrid = null;
        string? lastMealLocalTime = null;
        double? hoursSinceLastMeal = null;

        if (period == AnalysisPeriod.Day)
        {
            hourGrid = new List<string>();
            var startHour = (nowLocal.Minute == 0) ? nowLocal.Hour : nowLocal.Hour + 1;
            for (int h = Math.Min(startHour, 23); h <= 23; h++) hourGrid.Add($"{h:00}:00");

            if (meals.Any())
            {
                var lastLocal = meals
                    .Select(m => DateTime.ParseExact(m.LocalDateTimeIso, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                    .Max();

                lastMealLocalTime = lastLocal.ToString("HH:mm");
                hoursSinceLastMeal = (nowLocal.DateTime - lastLocal).TotalHours;
            }
        }

        var byHour = meals
            .GroupBy(m => int.Parse(m.LocalTime[..2], CultureInfo.InvariantCulture))
            .Select(g => new GroupByHour { Hour = g.Key, Count = g.Count(), Kcal = g.Sum(x => x.Calories) })
            .OrderBy(x => x.Hour)
            .ToList();

        var byDay = meals
            .GroupBy(m => m.LocalDate)
            .Select(g => new GroupByDay
            {
                Date = g.Key,
                Meals = g.Count(),
                Kcal = g.Sum(x => x.Calories),
                Proteins = g.Sum(x => x.Proteins),
                Fats = g.Sum(x => x.Fats),
                Carbs = g.Sum(x => x.Carbs)
            })
            .OrderBy(x => x.Date)
            .ToList();

        var utcOffset = tz.GetUtcOffset(nowLocal.DateTime);
        var utcOffsetStr = utcOffset.ToString("hh\\:mm");
        var nowLocalStr = nowLocal.ToString("yyyy-MM-dd HH:mm");
        var nowLocalHourStr = nowLocal.ToString("HH");
        var nowLocalDateStr = nowLocal.ToString("yyyy-MM-dd");
        var periodKindStr = period.ToString();
        var periodStartLocalStr = periodStartLocal.ToString("yyyy-MM-dd HH:mm");
        var periodEndLocalStr = nowLocalStr;
        var periodStartUtcStr = periodStartUtc.ToString("yyyy-MM-dd HH:mm:ss");
        var periodEndUtcStr = nowUtc.ToString("yyyy-MM-dd HH:mm:ss");

        var data = new ReportPayload
        {
            Timezone = new TimezoneInfoPayload { Id = tz.Id, Label = "Europe/Moscow / Russian Standard Time", UtcOffset = utcOffsetStr },
            Period = new PeriodInfoPayload
            {
                Kind = periodKindStr,
                Label = periodHuman,
                StartLocal = periodStartLocalStr,
                EndLocal = periodEndLocalStr,
                StartUtc = periodStartUtcStr,
                EndUtc = periodEndUtcStr
            },
            Now = new NowInfoPayload { Local = nowLocalStr, LocalHour = nowLocalHourStr, LocalDate = nowLocalDateStr },
            Client = new ClientInfoPayload { Name = card?.Name, Age = age, Goals = card?.DietGoals, Restrictions = card?.MedicalRestrictions },
            Meals = meals,
            Totals = totals,
            Grouping = new Grouping { ByHour = byHour, ByDay = byDay },
            DailyPlanContext = new DailyPlanContext { IsDaily = period == AnalysisPeriod.Day, RemainingHourGrid = hourGrid, LastMealLocalTime = lastMealLocalTime, HoursSinceLastMeal = hoursSinceLastMeal }
        };

        return new ReportData<ReportPayload>
        {
            Data = data,
            Json = JsonSerializer.Serialize(data),
            PeriodHuman = periodHuman
        };
    }

    private static (DateTimeOffset startUtc, string periodHuman, string recScopeHint, DateTime periodStartLocal)
        GetPeriodStart(DateTimeOffset nowLocal, AnalysisPeriod period, TimeZoneInfo tz)
    {
        switch (period)
        {
            case AnalysisPeriod.Day:
                {
                    var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала дня", "остаток дня", startLocal);
                }
            case AnalysisPeriod.Week:
                {
                    int dowRaw = (int)nowLocal.DayOfWeek; // Sunday=0
                    int isoDow = (dowRaw == 0) ? 7 : dowRaw;
                    int daysFromMonday = isoDow - 1;

                    var mondayLocalDate = nowLocal.Date.AddDays(-daysFromMonday);
                    var startLocal = new DateTime(mondayLocalDate.Year, mondayLocalDate.Month, mondayLocalDate.Day, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала недели", "неделю", startLocal);
                }
            case AnalysisPeriod.Month:
                {
                    var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала месяца", "месяц", startLocal);
                }
            case AnalysisPeriod.Quarter:
                {
                    var q = (nowLocal.Month - 1) / 3;
                    var startMonth = (q * 3) + 1;
                    var startLocal = new DateTime(nowLocal.Year, startMonth, 1, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала квартала", "квартал", startLocal);
                }
            default:
                {
                    var d = nowLocal.AddDays(-90).Date;
                    var startLocal = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала периода", "период", startLocal);
                }
        }
    }

    private static TimeZoneInfo GetMoscowTz()
    {
        string[] ids = new[] { "Europe/Moscow", "Russian Standard Time" };
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "UTC+03", "UTC+03");
    }
}
