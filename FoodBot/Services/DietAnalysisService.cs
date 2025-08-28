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

    /// <summary>
    /// Ежедневный отчёт: анализ уже съеденного с начала СЕГОДНЯ (локально МСК) + почасовой план на остаток дня
    /// </summary>
    public async Task<AnalysisReport> GetDailyAsync(long chatId, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, MoscowTz);

        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
        var dayStartLocalOffset = new DateTimeOffset(dayStartLocal, MoscowTz.GetUtcOffset(dayStartLocal));
        var dayStartUtc = dayStartLocalOffset.ToUniversalTime();

        // Кэш дневного отчёта по ЛОКАЛЬНОЙ дате (МСК)
        var localDayDateOnly = dayStartLocal.Date;
        var existing = await _db.AnalysisReports
            .FirstOrDefaultAsync(r => r.ChatId == chatId && r.ReportDate == localDayDateOnly, ct);

        // Был ли новый приём пищи после формирования отчёта?
        var lastMealTimeUtc = await _db.Meals
            .Where(m => m.ChatId == chatId && m.CreatedAtUtc >= dayStartUtc.UtcDateTime)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => (DateTimeOffset?)m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing != null && (!lastMealTimeUtc.HasValue || lastMealTimeUtc <= existing.CreatedAtUtc))
            return existing;

        // Либо отчёта ещё не было, либо появились новые приёмы — формируем заново
        var rec = existing ?? new AnalysisReport
        {
            ChatId = chatId,
            ReportDate = localDayDateOnly
        };

        rec.IsProcessing = true;
        rec.CreatedAtUtc = DateTimeOffset.UtcNow;

        if (existing == null)
            _db.AnalysisReports.Add(rec);

        await _db.SaveChangesAsync(ct);

        try
        {
            var markdown = await GenerateReportAsync(chatId, AnalysisPeriod.Day, ct);
            rec.Markdown = markdown;
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

    /// <summary>
    /// План-отчёт для недели/месяца/квартала: анализ уже съеденного с начала ПЕРИОДА (локально МСК) + общие рекомендации
    /// </summary>
    public async Task<string> GetPlanAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        if (period == AnalysisPeriod.Day)
            throw new ArgumentException("Day period is handled by GetDailyAsync", nameof(period));

        return await GenerateReportAsync(chatId, period, ct);
    }

    /// <summary>
    /// Единая генерация отчёта для любого периода.
    /// История собирается с НАЧАЛА ПЕРИОДА по ЛОКАЛЬНОМУ (Москва) времени → запрос к БД по UTC.
    /// </summary>
    private async Task<string> GenerateReportAsync(long chatId, AnalysisPeriod period, CancellationToken ct)
    {
        var tz = MoscowTz;
        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        // Начало периода (локально и UTC)
        var (periodStartUtc, periodHuman, recScopeHint, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);

        // Данные пользователя
        var card = await _db.PersonalCards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

        int? age = null;
        if (card?.BirthYear is int by && by > 1900 && by <= nowLocal.Year)
            age = nowLocal.Year - by;

        // История приёмов (UTC в БД) → локализация для тайминга/показа
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

        // Итоги на сейчас
        var totals = new
        {
            calories = meals.Sum(x => x.calories),
            proteins = meals.Sum(x => x.proteins),
            fats = meals.Sum(x => x.fats),
            carbs = meals.Sum(x => x.carbs),
            mealsCount = meals.Count()
        };

        // Для дневных рекомендаций: сетка часов до конца дня и время последнего приёма
        List<string>? hourGrid = null;
        string? lastMealLocalTime = null;
        double? hoursSinceLastMeal = null;

        if (period == AnalysisPeriod.Day)
        {
            hourGrid = new List<string>();
            // Если уже 23:xx — слотов не остаётся
            if (!(nowLocal.Hour == 23 && nowLocal.Minute > 0))
            {
                int startHour = (nowLocal.Minute == 0) ? nowLocal.Hour : nowLocal.Hour + 1;
                for (int h = startHour; h <= 23; h++)
                    hourGrid.Add($"{h:00}:00");
            }

            if (meals.Any())
            {
                var lastLocal = meals
                    .Select(m => DateTime.ParseExact(m.localDateTimeIso, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                    .Max();

                lastMealLocalTime = lastLocal.ToString("HH:mm");
                hoursSinceLastMeal = (nowLocal.DateTime - lastLocal).TotalHours;
            }
        }

        // Предварительные вычисления, чтобы не ловить method-group в инициализаторах
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

        // Структурированный контекст для модели
        var data = new
        {
            timezone = new
            {
                id = tz.Id,
                label = "Europe/Moscow / Russian Standard Time",
                utcOffset = utcOffsetStr
            },
            period = new
            {
                kind = periodKindStr,
                label = periodHuman,
                startLocal = periodStartLocalStr,
                endLocal = periodEndLocalStr,
                startUtc = periodStartUtcStr,
                endUtc = periodEndUtcStr
            },
            now = new
            {
                local = nowLocalStr,
                localHour = nowLocalHourStr,
                localDate = nowLocalDateStr
            },
            client = new
            {
                name = card?.Name,
                age,
                goals = card?.DietGoals,
                restrictions = card?.MedicalRestrictions
            },
            meals,
            totals,
            dailyPlanContext = new
            {
                isDaily = period == AnalysisPeriod.Day,
                remainingHourGrid = hourGrid,   // ["15:00","16:00",...,"23:00"]
                lastMealLocalTime,
                hoursSinceLastMeal
            }
        };

        // Инструкции для модели
        var instructionsRu =
$@"Ты — внимательный клинический нутрициолог.
Анализируй ТОЛЬКО фактически съеденное с начала периода до текущего момента.
Важно: весь анализ и рекомендации привязывай к локальному времени пользователя (Москва, UTC+3). Учитывай время каждого приёма (рано/поздно, интервалы, поздние углеводы и т.п.).

Сформируй ответ на русском в Markdown с разделами:

## Что уже съедено ({periodHuman})
Таблица с колонками:
Дата | Время (МСК) | Блюдо | Ккал | Б, г | Ж, г | У, г | Комментарий диетолога
— В комментарии кратко оцени блюдо: сытность, баланс БЖУ, клетчатка, соль/сахар, соответствие целям/ограничениям, уместность по времени суток.

## Итоги периода на сейчас
- Суммарно: Ккал, Б/Ж/У и доли макронутриентов (%).
- Кол-во приёмов.
- Краткие выводы (перекосы, дефициты, повторы, интервалы между приёмами).

## Комментарии к уже потреблённым Ккал и БЖУ
- Оцени темп потребления к текущему часу (с учётом времени суток), где явные перекосы.
- Если данных мало, укажи, чего не хватает для корректного вывода.

## Индивидуальные нюансы
Учти возраст, цели и ограничения пользователя; укажи, как это влияет на советы.

## Рекомендации
- Если период = день → **План на остаток дня (по часам МСК)**:
  - Пройди по часовой сетке из входных данных (remainingHourGrid) и для КАЖДОГО часа явно напиши один из вариантов:
    - «не есть» (если это разумно),
    - «вода/чай/кофе без сахара»,
    - «перекус» (с примерами и ориентиром по Ккал/БЖУ),
    - «приём пищи» (пример состава и ориентиры по БЖУ/Ккал).
  - Укажи целевые ориентиры БЖУ на остаток дня агрегировано (что добрать/срезать).
  - Если последний приём был недавно, подчеркни минимальный интервал до следующего.
- Если период = неделя/месяц/квартал → **Рекомендации на {recScopeHint}**: более общие принципы, паттерны, план закупок/заготовок, чек-лист.

Требования к стилю:
- Конкретные пункты без воды; числа там, где уместно.
- Без клинических диагнозов и процедур, требующих очного врача.
";

        var periodPrompt = period switch
        {
            AnalysisPeriod.Day => "Сформируй почасовой план на остаток текущего дня, используя переданную сетку часов (remainingHourGrid).",
            AnalysisPeriod.Week => "Сформируй общие рекомендации на текущую неделю.",
            AnalysisPeriod.Month => "Сформируй общие рекомендации на текущий месяц.",
            AnalysisPeriod.Quarter => "Сформируй общие рекомендации на текущий квартал.",
            _ => "Сформируй рекомендации на указанный период."
        };

        // Запрос к OpenAI Responses API
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

    /// <summary>
    /// Возвращает:
    ///  - начало периода в UTC для выборки из БД,
    ///  - человекочитаемую подпись периода,
    ///  - подсказку для блока рекомендаций,
    ///  - начало периода в ЛОКАЛИ (для отображения/контекста).
    /// Неделя — ISO (понедельник первый).
    /// </summary>
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
                    // ISO: Monday=1 ... Sunday=7
                    int dowRaw = (int)nowLocal.DayOfWeek;      // Sunday=0
                    int isoDow = (dowRaw == 0) ? 7 : dowRaw;   // Monday=1..Sunday=7
                    int daysFromMonday = isoDow - 1;           // 0..6

                    var mondayLocalDate = nowLocal.Date.AddDays(-daysFromMonday); // DateTime
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
                    var q = (nowLocal.Month - 1) / 3;      // 0..3
                    var startMonth = (q * 3) + 1;          // 1,4,7,10
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

    /// <summary>
    /// Возврат часового пояса Москва кросс-платформенно:
    /// - Linux/macOS: "Europe/Moscow"
    /// - Windows: "Russian Standard Time"
    /// </summary>
    private static TimeZoneInfo GetMoscowTz()
    {
        string[] ids = new[] { "Europe/Moscow", "Russian Standard Time" };
        foreach (var id in ids)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        // Фоллбэк: фиксированный UTC+3 (без переходов)
        return TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "UTC+03", "UTC+03");
    }
}
