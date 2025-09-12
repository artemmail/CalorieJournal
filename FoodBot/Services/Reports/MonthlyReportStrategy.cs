using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class MonthlyReportStrategy : ReportStrategyBase
{
    public MonthlyReportStrategy(
        ReportDataLoader loader,
        MonthlyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Month, loader, promptBuilder, generator)
    {
    }
}
