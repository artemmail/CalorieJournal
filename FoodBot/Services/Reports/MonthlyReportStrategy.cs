using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class MonthlyReportStrategy : ReportStrategyBase<MonthlyReportData>
{
    public MonthlyReportStrategy(
        IReportDataLoader<MonthlyReportData> loader,
        MonthlyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Month, loader, promptBuilder, generator)
    {
    }
}
