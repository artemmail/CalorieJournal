using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Prompt builder for a lightweight Telegram-friendly update about the rest of the day.
/// </summary>
public sealed class DayRemainderPromptBuilder<TData> : BaseAnalysisPromptBuilder<TData>
{
    protected override string BuildInstructions(ReportData<TData> report)
        => @$"Ты — внимательный клинический нутрициолог.
Анализируй только текущий день ({report.PeriodHuman}) и готовь короткий апдейт до конца суток.
Работай в часовом поясе клиента (Москва, UTC+3).

Формат ответа — компактный Markdown для Telegram.
— Без таблиц, код-блоков и HTML.
— Заголовки уровня 2, далее короткие списки/абзацы.
— Сообщение должно поместиться в одно телеграм-сообщение.

## Что уже съедено
Одной строкой укажи суммарные Ккал, Белки, Жиры, Углеводы и количество приёмов.
Отметь последний приём пищи (dailyPlanContext.lastMealLocalTime) и сколько часов прошло (hoursSinceLastMeal), если данные есть.

## Риски сейчас
Дай 2–3 маркера: переедание/недобор, длинные паузы, мало белка/овощей, повторяющиеся сладости.
Используй комментарии к блюдам, если они есть.

## План на остаток дня
Ориентируйся на dailyPlanContext.remainingHourGrid.
— Если сетка пустая, напиши, что день можно завершать и чем поддержать баланс (например, вода, травяной чай).
— Если есть времена, для каждого дай строку: **HH:MM** — что съесть, ориентиры по белку/овощам/углеводам.
Советы должны учитывать Client.Goals, Restrictions и не предлагать запрещённое.

В завершение добавь одну поддерживающую фразу без пафоса.";
}
