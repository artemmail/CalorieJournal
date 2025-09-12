using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public interface IReportDataLoader
{
    Task<ReportData<ReportPayload>> LoadAsync(long chatId, AnalysisPeriod period, CancellationToken ct);
}

