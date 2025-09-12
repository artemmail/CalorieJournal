using FoodBot.Models;

namespace FoodBot.Services;

public sealed class DailyReportStrategy : IReportStrategy
{
    public AnalysisPeriod Period => AnalysisPeriod.Day;
    public IPromptBuilder PromptBuilder { get; }
    public DailyReportStrategy(DailyPromptBuilder builder) => PromptBuilder = builder;
}
