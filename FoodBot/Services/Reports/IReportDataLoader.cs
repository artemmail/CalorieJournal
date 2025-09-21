using System;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public interface IReportDataLoader<TData>
{
    Task<ReportData<TData>> LoadAsync(
        long chatId,
        AnalysisPeriod period,
        CancellationToken ct,
        DateOnly? periodStartLocalDate = null);
}
