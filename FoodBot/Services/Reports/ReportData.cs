namespace FoodBot.Services.Reports;

public class ReportData<T>
{
    /// <summary>Structured payload containing report information.</summary>
    public T Data { get; init; } = default!;

    /// <summary>Human readable description of the period covered by the report.</summary>
    public string PeriodHuman { get; init; } = string.Empty;
}

