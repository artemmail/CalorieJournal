namespace FoodBot.Services.Reports;

public class ReportData
{
    public object Data { get; init; } = default!;
    public string PeriodHuman { get; init; } = string.Empty;
}

public sealed class DailyReportData : ReportData { }
public sealed class WeeklyReportData : ReportData { }
public sealed class MonthlyReportData : ReportData { }
public sealed class QuarterlyReportData : ReportData { }

