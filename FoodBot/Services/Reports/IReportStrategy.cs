using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;

namespace FoodBot.Services.Reports;

public interface IReportStrategy
{
    AnalysisPeriod Period { get; }
    Task<object> LoadDataAsync(long chatId, CancellationToken ct);
    string BuildPrompt(object data);
    Task<string> GenerateAsync(object data, CancellationToken ct);
}
