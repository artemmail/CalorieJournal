using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public sealed class MonthlyDataLoader : ReportDataLoaderBase<MonthlyReportData>
{
    public MonthlyDataLoader(BotDbContext db) : base(db) { }
    protected override AnalysisPeriod Period => AnalysisPeriod.Month;
}

