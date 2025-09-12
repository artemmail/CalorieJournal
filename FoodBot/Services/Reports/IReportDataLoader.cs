using System.Threading;
using System.Threading.Tasks;

namespace FoodBot.Services.Reports;

public interface IReportDataLoader<TData>
{
    Task<TData> LoadAsync(long chatId, CancellationToken ct);
}

