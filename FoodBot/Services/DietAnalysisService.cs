using System;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FoodBot.Data;
using FoodBot.Models;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace FoodBot.Services;

public sealed class DietAnalysisService
{
    private readonly BotDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _apiKey;

    // Константа часового пояса: Москва (кросс-платформенно)
    private static TimeZoneInfo MoscowTz => GetMoscowTz();

    public DietAnalysisService(BotDbContext db, IHttpClientFactory httpFactory, IConfiguration cfg)
    {
        _db = db;
        _httpFactory = httpFactory;
        _apiKey = cfg["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey missing");
    }

    // === NEW: публичные методы для истории/очереди ===

    public async Task<List<AnalysisReport1>> ListReportsAsync(long chatId, CancellationToken ct)
    {
        await CleanupStaleAsync(ct); // авто-очистка "висящих"
        return await _db.AnalysisReports2.AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<AnalysisReport1?> GetReportAsync(long chatId, long id, CancellationToken ct)
    {
        return await _db.AnalysisReports2.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId && x.Id == id, ct);
    }

    /// <summary>Старт генерации отчёта (постановка в очередь) с проверкой checksum.</summary>
    public async Task<(string status, AnalysisReport1 report)> StartReportAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        var tz = MoscowTz;
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var (periodStartUtc, periodHuman, _, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);
        var periodStartLocalDate = DateOnly.FromDateTime(periodStartLocal);

        // Уже есть processing этого периода?
        var existingProcessing = await _db.AnalysisReports2
            .FirstOrDefaultAsync(r => r.ChatId == chatId && r.Period == period
                && r.PeriodStartLocalDate == periodStartLocalDate && r.IsProcessing, ct);

        if (existingProcessing != null)
            return ("processing", existingProcessing);

        // Посчитать текущую сумму калорий за период
        var currentChecksum = await ComputeCaloriesChecksum(chatId, periodStartUtc.UtcDateTime, ct);

        // Последний готовый отчёт этого периода в этом же периоде дат
        var lastReady = await _db.AnalysisReports2.AsNoTracking()
            .Where(r => r.ChatId == chatId && r.Period == period
                && r.PeriodStartLocalDate == periodStartLocalDate
                && !r.IsProcessing && r.Markdown != null)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (lastReady != null && lastReady.CaloriesChecksum == currentChecksum)
        {
            // Ничего не поменялось — говорим об этом клиенту
            var phantom = new AnalysisReport1
            {
                Id = lastReady.Id,
                ChatId = chatId,
                Period = period,
                PeriodStartLocalDate = periodStartLocalDate,
                Name = lastReady.Name ?? BuildName(period, periodStartLocalDate),
                CreatedAtUtc = lastReady.CreatedAtUtc,
                CaloriesChecksum = lastReady.CaloriesChecksum,
                Markdown = lastReady.Markdown,
                IsProcessing = false
            };
            return ("no_changes", phantom);
        }

        // Создаём запись "в обработке"
        var rec = new AnalysisReport1
        {
            ChatId = chatId,
            Period = period,
            PeriodStartLocalDate = periodStartLocalDate,
            Name = BuildName(period, periodStartLocalDate),
            IsProcessing = true,
            ProcessingStartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.AnalysisReports2.Add(rec);
        await _db.SaveChangesAsync(ct);

        return ("processing", rec);
    }

    /// <summary>Сгенерировать отчёт для конкретной записи (используется воркером очереди).</summary>
    public async Task GenerateForRecordAsync(long reportId, CancellationToken ct)
    {
        var rec = await _db.AnalysisReports2.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (rec == null) return;
        if (!rec.IsProcessing) return; // уже не нужно

        var tz = MoscowTz;
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        var (periodStartUtc, _, _, _) = GetPeriodStart(nowLocal, rec.Period, tz);

        // Генерация
        try
        {
            var markdown = await GenerateReportAsync(rec.ChatId, rec.Period, ct);
            // Обновить чек-сумму на момент завершения
            var checksum = await ComputeCaloriesChecksum(rec.ChatId, periodStartUtc.UtcDateTime, ct);

            rec.Markdown = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(rec.Period, nowLocal) : markdown;
            rec.CaloriesChecksum = checksum;
            rec.IsProcessing = false;
            rec.CreatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // При ошибке — снимаем флаг и удаляем запись, чтобы не висела
            _db.AnalysisReports2.Remove(rec);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    /// <summary>Удаляет все processing-отчёты (на запуске приложения по требованию ТЗ).</summary>
    public async Task CleanupAllProcessingOnStartupAsync(CancellationToken ct)
    {
        var bad = await _db.AnalysisReports2.Where(x => x.IsProcessing).ToListAsync(ct);
        if (bad.Count > 0)
        {
            _db.AnalysisReports2.RemoveRange(bad);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Удаляет зависшие processing (старше 10 минут).</summary>
    public async Task CleanupStaleAsync(CancellationToken ct)
    {
        var limit = DateTimeOffset.UtcNow.AddMinutes(-10);
        var stale = await _db.AnalysisReports2
            .Where(x => x.IsProcessing && x.ProcessingStartedAtUtc < limit)
            .ToListAsync(ct);

        if (stale.Count > 0)
        {
            _db.AnalysisReports2.RemoveRange(stale);
            await _db.SaveChangesAsync(ct);
        }
    }

    // === Старая совместимость (используется твоим текущим контроллером) ===

    public async Task<AnalysisReport1> GetDailyAsync(long chatId, CancellationToken ct)
    {
        var tz = MoscowTz;
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
        var dayStartLocalOffset = new DateTimeOffset(dayStartLocal, tz.GetUtcOffset(dayStartLocal));
        var dayStartUtc = dayStartLocalOffset.ToUniversalTime();

        var periodStartLocalDate = DateOnly.FromDateTime(dayStartLocal);

        var existing = await _db.AnalysisReports2
            .FirstOrDefaultAsync(r => r.ChatId == chatId
                                   && r.Period == AnalysisPeriod.Day
                                   && r.PeriodStartLocalDate == periodStartLocalDate, ct);

        var lastMealTimeUtc = await _db.Meals.AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= dayStartUtc.UtcDateTime)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => (DateTimeOffset?)m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing != null && (!lastMealTimeUtc.HasValue || lastMealTimeUtc <= existing.CreatedAtUtc))
            return existing;

        var rec = existing ?? new AnalysisReport1
        {
            ChatId = chatId,
            Period = AnalysisPeriod.Day,
            PeriodStartLocalDate = periodStartLocalDate,
            Name = BuildName(AnalysisPeriod.Day, periodStartLocalDate)
        };

        rec.IsProcessing = true;
        rec.ProcessingStartedAtUtc = DateTimeOffset.UtcNow;
        rec.CreatedAtUtc = DateTimeOffset.UtcNow;

        if (existing == null)
            _db.AnalysisReports2.Add(rec);

        await _db.SaveChangesAsync(ct);

        try
        {
            var markdown = await GenerateReportAsync(chatId, AnalysisPeriod.Day, ct);
            var checksum = await ComputeCaloriesChecksum(chatId, dayStartUtc.UtcDateTime, ct);

            rec.Markdown = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(AnalysisPeriod.Day, nowLocal) : markdown;
            rec.CaloriesChecksum = checksum;
            rec.IsProcessing = false;
            rec.CreatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            rec.IsProcessing = false;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        return rec;
    }

    public async Task<string> GetPlanAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        if (period == AnalysisPeriod.Day)
            throw new ArgumentException("Day period is handled by GetDailyAsync", nameof(period));

        var tz = MoscowTz;
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var (periodStartUtc, _, _, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);
        var periodStartLocalDate = DateOnly.FromDateTime(periodStartLocal);

        var existing = await _db.AnalysisReports2.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ChatId == chatId
                                   && r.Period == period
                                   && r.PeriodStartLocalDate == periodStartLocalDate, ct);

        var todayLocalStart = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
        var todayLocalStartOffset = new DateTimeOffset(todayLocalStart, tz.GetUtcOffset(todayLocalStart));
        var todayStartUtc = todayLocalStartOffset.ToUniversalTime();

        if (existing != null && existing.CreatedAtUtc >= todayStartUtc)
            return existing.Markdown ?? string.Empty;

        var updatable = await _db.AnalysisReports2
            .FirstOrDefaultAsync(r => r.ChatId == chatId
                                   && r.Period == period
                                   && r.PeriodStartLocalDate == periodStartLocalDate, ct);

        if (updatable == null)
        {
            updatable = new AnalysisReport1
            {
                ChatId = chatId,
                Period = period,
                PeriodStartLocalDate = periodStartLocalDate,
                Name = BuildName(period, periodStartLocalDate)
            };
            _db.AnalysisReports2.Add(updatable);
        }

        updatable.IsProcessing = true;
        updatable.ProcessingStartedAtUtc = DateTimeOffset.UtcNow;
        updatable.CreatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            var markdown = await GenerateReportAsync(chatId, period, ct);
            var checksum = await ComputeCaloriesChecksum(chatId, periodStartUtc.UtcDateTime, ct);

            var final = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(period, nowLocal) : markdown;
            updatable.Markdown = final;
            updatable.CaloriesChecksum = checksum;
            updatable.IsProcessing = false;
            updatable.CreatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return final;
        }
        catch
        {
            updatable.IsProcessing = false;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    // === Генерация отчёта (как у тебя), + фоллбэк если пусто ===

    private async Task<string> GenerateReportAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        var tz = MoscowTz;
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var (periodStartUtc, periodHuman, recScopeHint, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);

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
            return new
            {
                dish = m.DishName,
                localDate = local.ToString("yyyy-MM-dd"),
                localTime = local.ToString("HH:mm"),
                localDateTimeIso = local.ToString("yyyy-MM-dd HH:mm"),
                calories = m.CaloriesKcal ?? 0,
                proteins = m.ProteinsG ?? 0,
                fats = m.FatsG ?? 0,
                carbs = m.CarbsG ?? 0
            };
        }).ToList();

        var totals = new
        {
            calories = meals.Sum(x => x.calories),
            proteins = meals.Sum(x => x.proteins),
            fats = meals.Sum(x => x.fats),
            carbs = meals.Sum(x => x.carbs),
            mealsCount = meals.Count()
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
                    .Select(m => DateTime.ParseExact(m.localDateTimeIso, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                    .Max();

                lastMealLocalTime = lastLocal.ToString("HH:mm");
                hoursSinceLastMeal = (nowLocal.DateTime - lastLocal).TotalHours;
            }
        }

        var byHour = meals
            .GroupBy(m => int.Parse(m.localTime[..2], CultureInfo.InvariantCulture))
            .Select(g => new { hour = g.Key, cnt = g.Count(), kcal = g.Sum(x => x.calories) })
            .OrderBy(x => x.hour)
            .ToList();

        var byDay = meals
            .GroupBy(m => m.localDate)
            .Select(g => new
            {
                date = g.Key,
                meals = g.Count(),
                kcal = g.Sum(x => x.calories),
                prot = g.Sum(x => x.proteins),
                fat = g.Sum(x => x.fats),
                carb = g.Sum(x => x.carbs)
            })
            .OrderBy(x => x.date)
            .ToList();

        var utcOffset = tz.GetUtcOffset(nowLocal.DateTime);
        var utcOffsetStr = utcOffset.ToString(@"hh\:mm");
        var nowLocalStr = nowLocal.ToString("yyyy-MM-dd HH:mm");
        var nowLocalHourStr = nowLocal.ToString("HH");
        var nowLocalDateStr = nowLocal.ToString("yyyy-MM-dd");
        var periodKindStr = period.ToString();
        var periodStartLocalStr = periodStartLocal.ToString("yyyy-MM-dd HH:mm");
        var periodEndLocalStr = nowLocalStr;
        var periodStartUtcStr = periodStartUtc.ToString("yyyy-MM-dd HH:mm:ss");
        var periodEndUtcStr = nowUtc.ToString("yyyy-MM-dd HH:mm:ss");

        var data = new
        {
            timezone = new { id = tz.Id, label = "Europe/Moscow / Russian Standard Time", utcOffset = utcOffsetStr },
            period = new
            {
                kind = periodKindStr,
                label = periodHuman,
                startLocal = periodStartLocalStr,
                endLocal = periodEndLocalStr,
                startUtc = periodStartUtcStr,
                endUtc = periodEndUtcStr
            },
            now = new { local = nowLocalStr, localHour = nowLocalHourStr, localDate = nowLocalDateStr },
            client = new { name = card?.Name, age, goals = card?.DietGoals, restrictions = card?.MedicalRestrictions },
            meals,
            totals,
            grouping = new { byHour, byDay },
            dailyPlanContext = new { isDaily = period == AnalysisPeriod.Day, remainingHourGrid = hourGrid, lastMealLocalTime, hoursSinceLastMeal }
        };

        var instructionsRu =
$@"Ты — внимательный клинический нутрициолог.
Анализируй ТОЛЬКО фактически съеденное с начала периода до текущего момента.
Весь анализ привязывай к локальному времени пользователя (Москва, UTC+3). Учитывай время каждого приёма.

## Что уже съедено ({periodHuman})
Таблица: Дата | Время | Блюдо | Ккал | Б | Ж | У | Комментарий.

## Итоги периода на сейчас
Сумма Ккал, Б/Ж/У, кол-во приёмов, краткие выводы.

## Общая характеристика стиля питания
Тайминг, привычки, калорийность, БЖУ, что поменять (4–7 пунктов).

## Индивидуальные нюансы
Учитывай возраст, цели, ограничения.

## Рекомендации
- Если день → почасовой план на остаток дня (по remainingHourGrid).
- Если неделя/месяц/квартал → общие рекомендации на период; план на конец дня НЕ нужен.
Стиль — конкретно и без воды.
";

        var periodPrompt = period switch
        {
            AnalysisPeriod.Day => "Сформируй почасовой план на остаток текущего дня, используя remainingHourGrid.",
            AnalysisPeriod.Week => "Сформируй общие рекомендации на текущую неделю. План на конец дня не нужен.",
            AnalysisPeriod.Month => "Сформируй общие рекомендации на текущий месяц. План на конец дня не нужен.",
            AnalysisPeriod.Quarter => "Сформируй общие рекомендации на текущий квартал. План на конец дня не нужен.",
            _ => "Сформируй рекомендации на указанный период."
        };

        var reqObj = new
        {
            model = "gpt-4o-mini",
            input = new object[]
            {
                new { role = "system", content = "You are a helpful dietologist." },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = instructionsRu },
                        new { type = "input_text", text = periodPrompt },
                        new { type = "input_text", text = JsonSerializer.Serialize(data) }
                    }
                }
            }
        };

        var body = JsonSerializer.Serialize(reqObj);
        var http = _httpFactory.CreateClient();
        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(msg, ct);
        resp.EnsureSuccessStatusCode();
        var respText = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(respText);
        var content = doc.RootElement.GetProperty("output")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return content ?? string.Empty;
    }

    // === Утилиты ===

    private static string BuildName(AnalysisPeriod period, DateOnly startLocalDate)
    {
        var kind = period switch
        {
            AnalysisPeriod.Day => "день",
            AnalysisPeriod.Week => "неделя",
            AnalysisPeriod.Month => "месяц",
            AnalysisPeriod.Quarter => "квартал",
            _ => "период"
        };
        return $"{startLocalDate:yyyy-MM-dd} · {kind}";
    }

    private static string BuildFallbackMarkdown(AnalysisPeriod period, DateTimeOffset nowLocal)
    {
        var kind = period switch
        {
            AnalysisPeriod.Day => "день",
            AnalysisPeriod.Week => "неделя",
            AnalysisPeriod.Month => "месяц",
            AnalysisPeriod.Quarter => "квартал",
            _ => "период"
        };
        return
$@"# Отчёт ({kind})
Сформирован: {nowLocal:yyyy-MM-dd HH:mm}

_Автоматический фоллбэк: содержимое не пустое, но LLM не вернул ответ. Попробуй перегенерировать позже или проверить входные данные._";
    }

    private async Task<int> ComputeCaloriesChecksum(long chatId, DateTime periodStartUtc, CancellationToken ct)
    {
        var untilUtc = DateTime.UtcNow;
        var sum = await _db.Meals.AsNoTracking()
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= periodStartUtc && m.CreatedAtUtc <= untilUtc)
            .Select(m => m.CaloriesKcal ?? 0)
            .SumAsync(ct);

        return (int)sum;
    }

    // Возвращает начало периода (UTC/Local) и подписи
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
