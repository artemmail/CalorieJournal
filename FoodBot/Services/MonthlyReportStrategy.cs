using FoodBot.Models;

namespace FoodBot.Services;

public sealed class MonthlyReportStrategy : IReportStrategy
{
    public AnalysisPeriod Period => AnalysisPeriod.Month;
    public IPromptBuilder PromptBuilder { get; }
    public MonthlyReportStrategy(MonthlyPromptBuilder builder) => PromptBuilder = builder;
}
