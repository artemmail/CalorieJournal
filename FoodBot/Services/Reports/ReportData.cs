namespace FoodBot.Services.Reports;

public class ReportData
{
    /// <summary>Structured payload containing report information.</summary>
    public ReportPayload Data { get; init; } = new();

    /// <summary>Human readable description of the period covered by the report.</summary>
    public string PeriodHuman { get; init; } = string.Empty;
}

public sealed class DailyReportData : ReportData { }
public sealed class WeeklyReportData : ReportData { }
public sealed class MonthlyReportData : ReportData { }
public sealed class QuarterlyReportData : ReportData { }

