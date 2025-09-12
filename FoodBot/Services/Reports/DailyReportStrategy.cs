using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class DailyReportStrategy : ReportStrategyBase
{
    public DailyReportStrategy(
        ReportDataLoader loader,
        DailyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Day, loader, promptBuilder, generator)
    {
    }
}
