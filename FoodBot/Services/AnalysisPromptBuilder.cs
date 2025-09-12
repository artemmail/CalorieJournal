using System.Text.Json;
using FoodBot.Models;
using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Prompt builder that contains shared instructions for all analysis reports.
/// Period-specific guidance is selected based on <see cref="AnalysisPeriod"/>.
/// </summary>
public sealed class AnalysisPromptBuilder : IPromptBuilder
{
    private readonly string _periodPrompt;

    public AnalysisPromptBuilder(AnalysisPeriod period)
    {
        _periodPrompt = period switch
        {
            AnalysisPeriod.Day => "Сформируй почасовой план на остаток текущего дня, используя remainingHourGrid.",
            AnalysisPeriod.Week => "Сформируй общие рекомендации на текущую неделю. План на конец дня не нужен.",
            AnalysisPeriod.Month => "Сформируй общие рекомендации на текущий месяц. План на конец дня не нужен.",
            AnalysisPeriod.Quarter => "Сформируй общие рекомендации на текущий квартал. План на конец дня не нужен.",
            _ => string.Empty
        };
    }

    /// <summary>LLM model name or <c>null</c> to use default.</summary>
    public string? Model => "gpt-4o-mini";

    /// <inheritdoc />
    public string Build(object data)
    {
        if (data is not ReportData<ReportPayload> report)
            throw new ArgumentException("Invalid report data", nameof(data));

        var instructionsRu = @$"Ты — внимательный клинический нутрициолог.
Анализируй ТОЛЬКО фактически съеденное с начала периода до текущего момента.
Весь анализ привязывай к локальному времени пользователя (Москва, UTC+3). Учитывай время каждого приёма.

## Что уже съедено ({report.PeriodHuman})
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

        var reqObj = new
        {
            model = Model ?? "gpt-4o-mini",
            input = new object[]
            {
                new { role = "system", content = "You are a helpful dietologist." },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = instructionsRu },
                        new { type = "input_text", text = _periodPrompt },
                        new { type = "input_text", text = JsonSerializer.Serialize(report.Data) }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(reqObj);
    }
}
