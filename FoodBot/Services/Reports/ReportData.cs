namespace FoodBot.Services.Reports;

public class ReportData<T>
{
    /// <summary>Structured payload containing report information.</summary>
    public T Data { get; init; } = default!;

    /// <summary>JSON representation of <see cref="Data"/>.</summary>
    public string Json { get; init; } = string.Empty;

    /// <summary>Human readable description of the period covered by the report.</summary>
    public string PeriodHuman { get; init; } = string.Empty;
}

