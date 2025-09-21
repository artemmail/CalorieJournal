using System;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public interface IReportStrategy<TData>
{
    AnalysisPeriod Period { get; }

    /// <summary>
    /// Load structured report data for the given chat and period.
    /// </summary>
    Task<ReportData<TData>> LoadDataAsync(long chatId, DateOnly? periodStartLocalDate, CancellationToken ct);

    /// <summary>
    /// Build a prompt from the loaded report data.
    /// </summary>
    string BuildPrompt(ReportData<TData> data);

    /// <summary>
    /// Generate report markdown from a finished prompt.
    /// </summary>
    Task<string> GenerateAsync(string prompt, CancellationToken ct);
}
