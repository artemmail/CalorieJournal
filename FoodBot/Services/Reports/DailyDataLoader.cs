using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public sealed class DailyDataLoader : ReportDataLoaderBase<DailyReportData>
{
    public DailyDataLoader(BotDbContext db) : base(db) { }
    protected override AnalysisPeriod Period => AnalysisPeriod.Day;
}

