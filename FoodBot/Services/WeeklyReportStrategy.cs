using FoodBot.Models;

namespace FoodBot.Services;

public sealed class WeeklyReportStrategy : IReportStrategy
{
    public AnalysisPeriod Period => AnalysisPeriod.Week;
    public IPromptBuilder PromptBuilder { get; }
    public WeeklyReportStrategy(WeeklyPromptBuilder builder) => PromptBuilder = builder;
}
