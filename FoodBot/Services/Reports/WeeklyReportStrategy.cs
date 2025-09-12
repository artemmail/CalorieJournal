using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class WeeklyReportStrategy : ReportStrategyBase<WeeklyReportData>
{
    public WeeklyReportStrategy(
        IReportDataLoader<WeeklyReportData> loader,
        WeeklyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Week, loader, promptBuilder, generator)
    {
    }
}
