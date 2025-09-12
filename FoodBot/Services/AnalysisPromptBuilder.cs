using System.Text.Json;
using FoodBot.Models;

namespace FoodBot.Services;

/// <summary>
/// Base prompt builder that contains shared instructions for all analysis reports.
/// Concrete report builders override <see cref="PeriodPrompt"/> to provide
/// period-specific guidance and optionally <see cref="Model"/> for model name.
/// </summary>
public abstract class AnalysisPromptBuilder : IPromptBuilder
{
    /// <summary>LLM model name or <c>null</c> to use default.</summary>
    public virtual string? Model => "gpt-4o-mini";

    /// <summary>Gets period specific prompt instructions.</summary>
    protected abstract string PeriodPrompt { get; }

    /// <inheritdoc />
    public string Build(object data)
    {
        if (data is not ReportDataLoader.ReportData report)
            throw new ArgumentException("Invalid report data", nameof(data));

        var instructionsRu = $@"Ты — внимательный клинический нутрициолог.
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
                        new { type = "input_text", text = PeriodPrompt },
                        new { type = "input_text", text = JsonSerializer.Serialize(report.Data) }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(reqObj);
    }
}
