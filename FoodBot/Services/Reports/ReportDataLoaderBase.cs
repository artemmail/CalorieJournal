using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

/// <summary>
/// Base class for loading report data and wrapping it into <see cref="ReportData{T}"/>.
/// </summary>
public abstract class ReportDataLoaderBase<TData> : IReportDataLoader<TData>
{
    /// <inheritdoc />
    public async Task<ReportData<TData>> LoadAsync(
        long chatId,
        AnalysisPeriod period,
        CancellationToken ct,
        DateOnly? periodStartLocalDate = null)
    {
        var (payload, periodHuman) = await LoadCoreAsync(chatId, period, ct, periodStartLocalDate);
        return new ReportData<TData>
        {
            Data = payload,
            Json = JsonSerializer.Serialize(payload),
            PeriodHuman = periodHuman
        };
    }

    /// <summary>
    /// Implemented by derived classes to load the payload for the specified chat and period.
    /// </summary>
    protected abstract Task<(TData Data, string PeriodHuman)> LoadCoreAsync(
        long chatId,
        AnalysisPeriod period,
        CancellationToken ct,
        DateOnly? periodStartLocalDate = null);
}
