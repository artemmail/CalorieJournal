using System.Text.Json;
using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Builds prompts for weekly analysis reports.
/// Adds a per-day calories and macronutrients table as JSON.
/// </summary>
public sealed class WeekAnalysisPromptBuilder<TData> : BaseAnalysisPromptBuilder<TData>
{
    protected override string BuildInstructions(ReportData<TData> report)
        => @$"Ты — внимательный клинический нутрициолог.
Анализируй ТОЛЬКО фактически съеденное с начала недели до текущего момента.
Весь анализ привязывай к локальному времени пользователя (Москва, UTC+3). Учитывай время каждого приёма.

## Что уже съедено ({report.PeriodHuman})
Таблица: Дата | Время | Блюдо | Ккал | Б | Ж | У | Комментарий.

## Итоги недели
Сумма Ккал, Б/Ж/У, кол-во приёмов, краткие выводы.

## Баланс по дням
Используй отдельную таблицу JSON с Ккал и БЖУ по дням.

## Общая характеристика стиля питания
Тайминг, привычки, калорийность, БЖУ, что поменять (4–7 пунктов).

## Индивидуальные нюансы
Учитывай возраст, цели, ограничения.

## Рекомендации
Общие рекомендации на неделю. План на конец дня не нужен.
Стиль — конкретно и без воды.";

    protected override IEnumerable<object>? ExtraInputs(ReportData<TData> report)
    {
        if (report.Data is ReportPayload payload)
        {
            var table = payload.Grouping.ByDay.Select(d => new
            {
                date = d.Date,
                calories = d.Kcal,
                proteins = d.Proteins,
                fats = d.Fats,
                carbs = d.Carbs
            });

            return new[]
            {
                new
                {
                    type = "input_text",
                    text = JsonSerializer.Serialize(new { dailyTotals = table })
                }
            };
        }

        return null;
    }
}

