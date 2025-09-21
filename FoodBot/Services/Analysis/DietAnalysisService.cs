using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FoodBot.Data;
using FoodBot.Models;
using FoodBot.Services.Reports;
using System.Collections.Generic;


namespace FoodBot.Services;

public sealed class DietAnalysisService
{
    private readonly BotDbContext _db;
    private readonly IDictionary<AnalysisPeriod, IReportStrategy<ReportPayload>> _strategies;

    // Константа часового пояса: Москва (кросс-платформенно)
    private static TimeZoneInfo MoscowTz => GetMoscowTz();

    public DietAnalysisService(
        BotDbContext db,
        IDictionary<AnalysisPeriod, IReportStrategy<ReportPayload>> strategies)
    {
        _db = db;
        _strategies = strategies;
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
    public async Task<(string status, AnalysisReport1 report)> StartReportAsync(
        long chatId,
        AnalysisPeriod period,
        CancellationToken ct,
        DateOnly? periodStartLocalDateOverride = null)
    {
        var tz = MoscowTz;
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

        DateTime periodStartLocal;
        DateTimeOffset periodStartUtcOffset;

        if (periodStartLocalDateOverride.HasValue)
        {
            periodStartLocal = periodStartLocalDateOverride.Value.ToDateTime(TimeOnly.MinValue);
            var offset = new DateTimeOffset(periodStartLocal, tz.GetUtcOffset(periodStartLocal));
            periodStartUtcOffset = offset.ToUniversalTime();
        }
        else
        {
            (periodStartUtcOffset, _, _, periodStartLocal) = GetPeriodStart(nowLocal, period, tz);
        }

        var periodStartLocalDate = DateOnly.FromDateTime(periodStartLocal);
        DateTime? periodEndUtc = null;
        if (periodStartLocalDateOverride.HasValue && period == AnalysisPeriod.Day)
        {
            var endLocal = periodStartLocal.AddDays(1);
            var endOffset = new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal));
            periodEndUtc = endOffset.UtcDateTime;
        }

        // Уже есть processing этого периода?
        var existingProcessing = await _db.AnalysisReports2
            .FirstOrDefaultAsync(r => r.ChatId == chatId && r.Period == period
                && r.PeriodStartLocalDate == periodStartLocalDate && r.IsProcessing, ct);

        if (existingProcessing != null)
            return ("processing", existingProcessing);

        // Посчитать текущую сумму калорий за период
        var currentChecksum = await ComputeCaloriesChecksum(chatId, periodStartUtcOffset.UtcDateTime, ct, periodEndUtc);

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
        var periodStartLocal = rec.PeriodStartLocalDate.ToDateTime(TimeOnly.MinValue);
        var periodStartOffset = new DateTimeOffset(periodStartLocal, tz.GetUtcOffset(periodStartLocal));
        var periodStartUtc = periodStartOffset.ToUniversalTime();
        DateTime? periodEndUtc = null;
        if (rec.Period == AnalysisPeriod.Day)
        {
            var endLocal = periodStartLocal.AddDays(1);
            var endOffset = new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal));
            periodEndUtc = endOffset.UtcDateTime;
        }

        // Генерация
        try
        {
            var (markdown, requestJson) = await GenerateReportAsync(rec.ChatId, rec.Period, ct, rec.PeriodStartLocalDate);
            // Обновить чек-сумму на момент завершения
            var checksum = await ComputeCaloriesChecksum(rec.ChatId, periodStartUtc.UtcDateTime, ct, periodEndUtc);

            rec.Markdown = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(rec.Period, nowLocal) : markdown;
            rec.RequestJson = requestJson;
            rec.CaloriesChecksum = checksum;
            rec.IsProcessing = false;
            rec.CreatedAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
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
            var (markdown, requestJson) = await GenerateReportAsync(chatId, AnalysisPeriod.Day, ct, periodStartLocalDate);
            var checksum = await ComputeCaloriesChecksum(chatId, dayStartUtc.UtcDateTime, ct);

            rec.Markdown = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(AnalysisPeriod.Day, nowLocal) : markdown;
            rec.RequestJson = requestJson;
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
            var (markdown, requestJson) = await GenerateReportAsync(chatId, period, ct);
            var checksum = await ComputeCaloriesChecksum(chatId, periodStartUtc.UtcDateTime, ct);

            var final = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(period, nowLocal) : markdown;
            updatable.Markdown = final;
            updatable.RequestJson = requestJson;
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

    private async Task<(string Markdown, string RequestJson)> GenerateReportAsync(
        long chatId,
        AnalysisPeriod period,
        CancellationToken ct,
        DateOnly? periodStartLocalDate = null)
    {
        if (!_strategies.TryGetValue(period, out var strategy))
            throw new InvalidOperationException($"Strategy for period {period} not registered");
        var data = await strategy.LoadDataAsync(chatId, periodStartLocalDate, ct);
        var prompt = strategy.BuildPrompt(data);
        var markdown = await strategy.GenerateAsync(prompt, ct);
        return (markdown, prompt);
    }

    // === Утилиты ===

    private static string BuildName(AnalysisPeriod period, DateOnly startLocalDate)
    {
        var kind = period switch
        {
            AnalysisPeriod.Day => "день",
            AnalysisPeriod.DayRemainder => "день · остаток",
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
            AnalysisPeriod.DayRemainder => "день · остаток",
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

    private async Task<int> ComputeCaloriesChecksum(
        long chatId,
        DateTime periodStartUtc,
        CancellationToken ct,
        DateTime? periodEndUtc = null)
    {
        var untilUtc = periodEndUtc ?? DateTime.UtcNow;
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
            case AnalysisPeriod.DayRemainder:
                {
                    var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "с начала дня (до текущего момента)", "остаток дня", startLocal);
                }
            case AnalysisPeriod.Week:
                {
                    // Rolling window covering the last seven days including today
                    var startLocalDate = nowLocal.Date.AddDays(-6);
                    var startLocal = new DateTime(startLocalDate.Year, startLocalDate.Month, startLocalDate.Day, 0, 0, 0);
                    var startLocalOffset = new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal));
                    return (startLocalOffset.ToUniversalTime(), "за последние 7 дней", "7 дней", startLocal);
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
