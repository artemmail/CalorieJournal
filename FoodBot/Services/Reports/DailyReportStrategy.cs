using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class DailyReportStrategy : ReportStrategyBase<DailyReportData>
{
    public DailyReportStrategy(
        IReportDataLoader<DailyReportData> loader,
        DailyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Day, loader, promptBuilder, generator)
    {
    }
}
