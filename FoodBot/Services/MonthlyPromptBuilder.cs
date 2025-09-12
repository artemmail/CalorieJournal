namespace FoodBot.Services;

/// <summary>
/// Prompt builder for monthly analysis reports.
/// </summary>
public sealed class MonthlyPromptBuilder : AnalysisPromptBuilder
{
    protected override string PeriodPrompt => "Сформируй общие рекомендации на текущий месяц. План на конец дня не нужен.";
}
