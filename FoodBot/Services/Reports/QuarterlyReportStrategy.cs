using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class QuarterlyReportStrategy : ReportStrategyBase
{
    public QuarterlyReportStrategy(
        ReportDataLoader loader,
        QuarterlyPromptBuilder promptBuilder,
        AnalysisGenerator generator)
        : base(AnalysisPeriod.Quarter, loader, promptBuilder, generator)
    {
    }
}
