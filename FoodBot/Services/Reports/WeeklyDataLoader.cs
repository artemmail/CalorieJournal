using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public sealed class WeeklyDataLoader : ReportDataLoaderBase<WeeklyReportData>
{
    public WeeklyDataLoader(BotDbContext db) : base(db) { }
    protected override AnalysisPeriod Period => AnalysisPeriod.Week;
}

