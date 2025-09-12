using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public sealed class QuarterlyDataLoader : ReportDataLoaderBase<QuarterlyReportData>
{
    public QuarterlyDataLoader(BotDbContext db) : base(db) { }
    protected override AnalysisPeriod Period => AnalysisPeriod.Quarter;
}

