namespace FoodBot.Services;

/// <summary>
/// Prompt builder for weekly analysis reports.
/// </summary>
public sealed class WeeklyPromptBuilder : AnalysisPromptBuilder
{
    protected override string PeriodPrompt => "Сформируй общие рекомендации на текущую неделю. План на конец дня не нужен.";
}
