namespace FoodBot.Services;

/// <summary>
/// Prompt builder for daily analysis reports.
/// </summary>
public sealed class DailyPromptBuilder : AnalysisPromptBuilder
{
    protected override string PeriodPrompt => "Сформируй почасовой план на остаток текущего дня, используя remainingHourGrid.";
}
