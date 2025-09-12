namespace FoodBot.Services;

/// <summary>
/// Prompt builder for quarterly analysis reports.
/// </summary>
public sealed class QuarterlyPromptBuilder : AnalysisPromptBuilder
{
    protected override string PeriodPrompt => "Сформируй общие рекомендации на текущий квартал. План на конец дня не нужен.";
}
