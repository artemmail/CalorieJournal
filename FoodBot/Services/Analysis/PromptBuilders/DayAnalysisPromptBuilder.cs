using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Builds prompts for daily analysis reports (compact, Telegram-friendly),
/// с авто-расчётом окна питания и «поздних приёмов» на бэке.
/// </summary>
public sealed class DayAnalysisPromptBuilder<TData> : BaseAnalysisPromptBuilder<TData>
{
    protected override string BuildInstructions(ReportData<TData> report)
        => @$"Ты — внимательный клинический нутрициолог.
Анализируй ТОЛЬКО фактически съеденное за текущий день ({report.PeriodHuman}).
Работай в локальном времени клиента. Если в meta.timezone указана зона — используй её; иначе считай Москва (UTC+3).

Формат: компактный Markdown для Telegram.
— Короткие абзацы и списки, без таблиц, код-блоков и HTML.
— Ответ должен влезать в 1–2 экрана мобильного Telegram.
— Не перечисляй весь дневной рацион — только выводы и действия.
— Не выдумывай данные. Если чего-то нет — пиши «н/д».

У тебя уже есть предрассчитанные признаки в features (если они переданы бэкендом). Пользуйся ими, не пересчитывай заново.
Если какого-то признака нет — корректно сообщи «н/д».

## Итоги дня
Одной строкой: Ккал; Б/Ж/У; приёмов N; окно питания HH:MM–HH:MM (если есть).
Если есть targetCalories — добавь «∆ от цели: ±X ккал (±Y%)».
Если features.energyMismatchDetected = true — добавь ⚠ «обнаружены расхождения ккал/БЖУ в данных».

## Короткое резюме (до 3 пунктов)
— Тайминг: поздние приёмы (≥19:00/≥20:00), длинные паузы (>5 ч).
— Повторы: упомяни 1–2 частых блюда/продукта (по наименованиям, если заметно в данных/комментариях).
— Быстрые сахара/жирные добавки — только если явно прослеживаются по названиям.

## Что учесть клиенту (3–5 персональных советов)
Опирайся на цели/ограничения, возраст и вес (если есть).
Давай конкретику с числами, например:
— Завтрак ≥ 25–35 г белка; овощи ≥ 200 г/день; ужин до 20:00.
— Фрукты: 1 порция ≤ 150 г за приём; вода 1.5–2 л (или «начать фиксировать воду»).
— Жиры: заменить часть насыщённых на 1 ч. л. оливкового масла; рыба 2–3 р/нед (если уместно).

## План на завтра (3–4 шага)
Нумерованный список кратких действий с временем:
1) HH:MM — белковый завтрак (25–35 г): пример из привычных блюд.
2) В обед — 150–200 г овощей + сложные углеводы (если уместно).
3) Перекус с белком (йогурт/творог/орехи, 10–20 г белка).
4) Ужин до 20:00 (рыба/птица + овощи).
Не предлагай запрещённые продукты. Примеры — кратко, без рецептов.

## Итог (1 предложение)
Что получилось хорошо сегодня и на чём сфокусироваться завтра.";

    /// <summary>
    /// Передаём в модель цели/антропометрию/таймзону (meta) и предрасчитанные признаки дня (features):
    /// - windowStart/windowEnd (HH:mm), firstMeal/lastMeal, mealsCount
    /// - lateAfter19Count, lateAfter20Count
    /// - longestFastHours (макс. пауза между приёмами, часы, округление до 0.1)
    /// - energyMismatchDetected (bool) — есть ли заметные расхождения kcal vs 4*P+9*F+4*C по хотя бы одной позиции
    /// 
    /// Все вычисления делаем для целевого дня:
    /// 1) report.PeriodStart (если доступен) в локальной зоне meta.timezone
    /// 2) иначе — последняя дата в payload.Grouping.ByDay
    /// </summary>
    protected override IEnumerable<object>? ExtraInputs(ReportData<TData> report)
    {
        if (report.Data is not ReportPayload payload)
            return null;

        // meta (цели/профиль/таймзона)
        var meta = BuildMetaObject(payload);

        // Определяем целевую дату (без времени) в локальной зоне:
        var tz = meta.timezone ?? "Europe/Moscow";
        var localZone = ResolveTimeZoneInfo(tz);
        var targetDate = ResolveTargetDate(report, payload, localZone);

        // Собираем все "похожие на приём пищи" элементы этого дня
        var meals = ExtractMealsForDate(payload, targetDate, localZone);

        // Рассчитываем признаки
        var features = ComputeDayFeatures(meals, localZone);

        // Упаковываем в input_text
        var json = JsonSerializer.Serialize(new
        {
            meta,
            features
        });

        return new[]
        {
            new { type = "input_text", text = json }
        };
    }

    // ----------------------------- FEATURES LOGIC -----------------------------

    private static object ComputeDayFeatures(List<MealPoint> meals, TimeZoneInfo tz)
    {
        meals = meals
            .Where(m => m.LocalTime.HasValue)
            .OrderBy(m => m.LocalTime!.Value)
            .ToList();

        string? fmt(DateTimeOffset? dt) => dt?.ToString("HH:mm", CultureInfo.InvariantCulture);

        var mealsCount = meals.Count;
        DateTimeOffset? first = meals.FirstOrDefault()?.LocalTime;
        DateTimeOffset? last = meals.LastOrDefault()?.LocalTime;

        string? windowStart = fmt(first);
        string? windowEnd = fmt(last);

        int late19 = meals.Count(m => m.LocalTime!.Value.Hour >= 19);
        int late20 = meals.Count(m => m.LocalTime!.Value.Hour >= 20);

        // Макс. пауза
        double longestFastHours = 0.0;
        for (int i = 1; i < meals.Count; i++)
        {
            var gap = meals[i].LocalTime!.Value - meals[i - 1].LocalTime!.Value;
            if (gap.TotalHours > longestFastHours)
                longestFastHours = gap.TotalHours;
        }
        longestFastHours = Math.Round(longestFastHours, 1);

        // Флаг расхождений по энергетике: |kcal - (4P + 9F + 4C)| > 10% или > 30 ккал
        bool energyMismatchDetected = meals.Any(m =>
        {
            if (m.Kcal == null || (m.P == null && m.F == null && m.C == null))
                return false;

            double p = m.P ?? 0, f = m.F ?? 0, c = m.C ?? 0;
            double calc = 4 * p + 9 * f + 4 * c;
            double stated = m.Kcal ?? 0;
            double diff = Math.Abs(stated - calc);
            double threshold = Math.Max(30.0, 0.10 * Math.Max(1.0, calc));
            return diff > threshold;
        });

        return new
        {
            date = meals.FirstOrDefault()?.LocalTime?.Date, // локальная дата
            mealsCount,
            windowStart,
            windowEnd,
            firstMeal = windowStart,
            lastMeal = windowEnd,
            lateAfter19Count = late19,
            lateAfter20Count = late20,
            longestFastHours,
            energyMismatchDetected
        };
    }

    // ----------------------------- EXTRACTION -----------------------------

    /// <summary>
    /// Достаём из payload список «точек приёма пищи» на нужную локальную дату.
    /// Пытаемся находить коллекции по типичным именам, а затем по группировкам ByDay.
    /// </summary>
    private static List<MealPoint> ExtractMealsForDate(object payload, DateOnly targetDate, TimeZoneInfo tz)
    {
        // 1) Попробуем «плоские» коллекции в payload
        var flatCollections = new[]
        {
            "Items","Entries","Records","Meals","Intakes","FoodIntakes","All","Flat","List"
        };

        foreach (var name in flatCollections)
        {
            var seq = TryGetEnumerable(payload, name);
            if (seq != null)
            {
                var points = seq.Select(x => ToMealPoint(x, tz))
                                .Where(mp => mp.LocalTime.HasValue && DateOnly.FromDateTime(mp.LocalTime.Value.DateTime) == targetDate)
                                .ToList();
                if (points.Count > 0) return points;
            }
        }

        // 2) Ищем в Grouping.ByDay
        var grouping = TryGet<object>(payload, "Grouping");
        var byDay = TryGetEnumerable(grouping, "ByDay");
        if (byDay != null)
        {
            // Находим группу нужной даты
            foreach (var day in byDay)
            {
                var dateVal = TryGet<DateOnly?>(day, "Date")
                              ?? TryAsDateOnly(TryGet<DateTime?>(day, "Date"))
                              ?? TryAsDateOnly(TryGet<DateTimeOffset?>(day, "Date"));

                if (dateVal != targetDate) continue;

                // Внутри может быть Items/Entries/Records
                foreach (var name in flatCollections)
                {
                    var seq = TryGetEnumerable(day, name);
                    if (seq != null)
                    {
                        var points = seq.Select(x => ToMealPoint(x, tz))
                                        .Where(mp => mp.LocalTime.HasValue)
                                        .ToList();
                        if (points.Count > 0) return points;
                    }
                }
            }
        }

        // 3) Ничего не нашли — вернём пусто
        return new List<MealPoint>();
    }

    private static MealPoint ToMealPoint(object item, TimeZoneInfo tz)
    {
        // Время: предпочтительно локальное поле; иначе UTC + конверсия; иначе «как есть».
        DateTimeOffset? local = TryGet<DateTimeOffset?>(item, "LocalTime")
                                ?? TryGet<DateTimeOffset?>(item, "TimeLocal")
                                ?? TryGet<DateTimeOffset?>(item, "AtLocal")
                                ?? TryGet<DateTimeOffset?>(item, "DateTimeLocal");

        if (local == null)
        {
            var localDt = TryGet<DateTime?>(item, "LocalTime")
                          ?? TryGet<DateTime?>(item, "TimeLocal")
                          ?? TryGet<DateTime?>(item, "AtLocal")
                          ?? TryGet<DateTime?>(item, "DateTimeLocal");
            if (localDt != null)
                local = new DateTimeOffset(localDt.Value, tz.GetUtcOffset(localDt.Value));
        }

        if (local == null)
        {
            // Попробуем UTC и переведём в локальную зону
            var utc = TryGet<DateTimeOffset?>(item, "AtUtc")
                      ?? TryGet<DateTimeOffset?>(item, "CreatedAtUtc")
                      ?? TryGet<DateTimeOffset?>(item, "TimestampUtc");

            if (utc == null)
            {
                var utcDt = TryGet<DateTime?>(item, "AtUtc")
                            ?? TryGet<DateTime?>(item, "CreatedAtUtc")
                            ?? TryGet<DateTime?>(item, "TimestampUtc");

                if (utcDt != null)
                    utc = new DateTimeOffset(DateTime.SpecifyKind(utcDt.Value, DateTimeKind.Utc));
            }

            if (utc != null)
            {
                var converted = TimeZoneInfo.ConvertTime(utc.Value, tz);
                local = converted;
            }
        }

        if (local == null)
        {
            // Возьмём "как есть" (предположительно, уже локальное)
            var dt = TryGet<DateTimeOffset?>(item, "At")
                     ?? TryGet<DateTimeOffset?>(item, "Time")
                     ?? TryGet<DateTimeOffset?>(item, "DateTime");

            if (dt == null)
            {
                var dt2 = TryGet<DateTime?>(item, "At")
                          ?? TryGet<DateTime?>(item, "Time")
                          ?? TryGet<DateTime?>(item, "DateTime")
                          ?? TryGet<DateTime?>(item, "CreatedAt");
                if (dt2 != null)
                    dt = new DateTimeOffset(dt2.Value, tz.GetUtcOffset(dt2.Value));
            }

            local = dt;
        }

        // Макросы/ккал (для flag)
        double? kcal = TryGetDouble(item, "Kcal", "Calories", "EnergyKcal");
        double? p = TryGetDouble(item, "Proteins", "Protein", "P");
        double? f = TryGetDouble(item, "Fats", "Fat", "F");
        double? c = TryGetDouble(item, "Carbs", "Carbohydrates", "C");

        return new MealPoint
        {
            LocalTime = local,
            Kcal = kcal,
            P = p,
            F = f,
            C = c
        };
    }

    // ----------------------------- TARGET DATE / TIMEZONE -----------------------------

    private static DateOnly ResolveTargetDate(ReportData<TData> report, object payload, TimeZoneInfo tz)
    {
        // 1) Попробуем report.PeriodStart (если доступно через reflection)
        var periodStart = TryGet<DateTimeOffset?>(report, "PeriodStart")
                          ?? TryGet<DateTime?>(report, "PeriodStart")?.ToUniversalTime();

        if (periodStart != null)
        {
            var local = TimeZoneInfo.ConvertTime(periodStart.Value, tz);
            return DateOnly.FromDateTime(local.Date);
        }

        // 2) Последняя дата в Grouping.ByDay
        var grouping = TryGet<object>(payload, "Grouping");
        var byDay = TryGetEnumerable(grouping, "ByDay");
        if (byDay != null)
        {
            var dates = byDay.Select(d =>
                            TryGet<DateOnly?>(d, "Date")
                            ?? TryAsDateOnly(TryGet<DateTime?>(d, "Date"))
                            ?? TryAsDateOnly(TryGet<DateTimeOffset?>(d, "Date")))
                             .Where(x => x != null)
                             .Select(x => x!.Value)
                             .ToList();

            if (dates.Count > 0)
                return dates.Max();
        }

        // 3) Фоллбек: сегодня в локальной зоне
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(nowLocal.Date);
    }

    private static TimeZoneInfo ResolveTimeZoneInfo(string tz)
    {
        // Пытаемся найти как Windows ID, затем как IANA через простую мапу
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch { /* try iana mapping below */ }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Europe/Moscow", "Russian Standard Time" },
            { "Europe/Bucharest", "GTB Standard Time" },
            { "Europe/Kiev", "FLE Standard Time" },
            { "Europe/Kyiv", "FLE Standard Time" },
            { "Europe/Berlin", "W. Europe Standard Time" },
            { "Europe/London", "GMT Standard Time" },
            { "UTC", "UTC" }
        };

        if (map.TryGetValue(tz, out var windowsId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); } catch { }
        }

        // Попытка распарсить «UTC+3» / «UTC+03:00»
        if (tz.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
        {
            var signIdx = tz.IndexOf('+');
            if (signIdx < 0) signIdx = tz.IndexOf('-', StringComparison.OrdinalIgnoreCase);
            if (signIdx > 0)
            {
                var offs = tz.Substring(signIdx);
                if (TimeSpan.TryParse(offs.Replace("UTC", "", StringComparison.OrdinalIgnoreCase), out var ts))
                {
                    // Создадим кастомную зону с фиксированным смещением
                    return TimeZoneInfo.CreateCustomTimeZone($"UTC{offs}", ts, $"UTC{offs}", $"UTC{offs}");
                }
            }
        }

        // Фоллбек: Москва UTC+3
        return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
    }

    // ----------------------------- META -----------------------------

    /// <summary>
    /// meta: TargetCalories, Profile.{WeightKg, HeightCm, Age, Sex, Goal, Timezone}, Payload.Timezone.
    /// </summary>
    private static dynamic BuildMetaObject(object payload)
    {
        var targetCalories = TryGet<double?>(payload, "TargetCalories")
                             ?? TryGet<int?>(payload, "TargetCalories") as double?;

        var profile = TryGet<object>(payload, "Profile");

        var weightKg = TryGet<double?>(profile, "WeightKg") ?? TryGet<int?>(profile, "WeightKg") as double?;
        var heightCm = TryGet<double?>(profile, "HeightCm") ?? TryGet<int?>(profile, "HeightCm") as double?;
        var ageYears = TryGet<int?>(profile, "Age") ?? (int?)TryGet<double?>(profile, "Age");
        var sex = TryGet<string>(profile, "Sex") ?? TryGet<string>(profile, "Gender");
        var goal = TryGet<string>(profile, "Goal");
        var timezone = TryGet<string>(profile, "Timezone") ?? TryGet<string>(payload, "Timezone");

        return new
        {
            targetCalories,
            weightKg,
            heightCm,
            ageYears,
            sex,
            goal,
            timezone
        };
    }

    // ----------------------------- REFLECTION HELPERS -----------------------------

    private static T? TryGet<T>(object? obj, string propertyName)
    {
        if (obj == null) return default;
        var type = obj.GetType();
        var pi = type.GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (pi == null) return default;

        var value = pi.GetValue(obj);
        if (value == null) return default;

        try
        {
            if (value is T tv) return tv;

            var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            var converted = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            return (T?)converted;
        }
        catch { return default; }
    }

    private static IEnumerable<object>? TryGetEnumerable(object? obj, string propertyName)
    {
        var val = TryGet<object>(obj, propertyName);
        if (val == null) return null;

        if (val is string) return null; // строка — не коллекция

        if (val is IEnumerable enumerable)
        {
            var list = new List<object>();
            foreach (var it in enumerable)
                if (it != null) list.Add(it);
            return list;
        }
        return null;
    }

    private static DateOnly? TryAsDateOnly(DateTime? dt)
        => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : (DateOnly?)null;

    private static DateOnly? TryAsDateOnly(DateTimeOffset? dto)
        => dto.HasValue ? DateOnly.FromDateTime(dto.Value.LocalDateTime) : (DateOnly?)null;

    private static double? TryGetDouble(object? obj, params string[] names)
    {
        foreach (var n in names)
        {
            var d = TryGet<double?>(obj, n);
            if (d != null) return d;

            var i = TryGet<int?>(obj, n);
            if (i != null) return (double)i;

            var s = TryGet<string>(obj, n);
            if (s != null && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dv))
                return dv;
        }
        return null;
    }

    // ----------------------------- INTERNAL MODEL -----------------------------

    private sealed class MealPoint
    {
        public DateTimeOffset? LocalTime { get; set; }
        public double? Kcal { get; set; }
        public double? P { get; set; }
        public double? F { get; set; }
        public double? C { get; set; }
    }
}
