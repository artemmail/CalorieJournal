using FoodBot.Models;

namespace FoodBot.Services;

public sealed class QuarterlyReportStrategy : IReportStrategy
{
    public AnalysisPeriod Period => AnalysisPeriod.Quarter;
    public IPromptBuilder PromptBuilder { get; }
    public QuarterlyReportStrategy(QuarterlyPromptBuilder builder) => PromptBuilder = builder;
}
