using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class ReportStrategy<TData> : IReportStrategy<TData>
{
    private readonly IReportDataLoader<TData> _loader;
    private readonly IPromptBuilder<TData> _promptBuilder;
    private readonly AnalysisGenerator _generator;

    public ReportStrategy(AnalysisPeriod period, IReportDataLoader<TData> loader, IPromptBuilder<TData> promptBuilder, AnalysisGenerator generator)
    {
        Period = period;
        _loader = loader;
        _promptBuilder = promptBuilder;
        _generator = generator;
    }

    public AnalysisPeriod Period { get; }

    public async Task<ReportData<TData>> LoadDataAsync(long chatId, CancellationToken ct)
        => await _loader.LoadAsync(chatId, Period, ct);

    public string BuildPrompt(ReportData<TData> data)
        => _promptBuilder.Build(data);

    public Task<string> GenerateAsync(string prompt, CancellationToken ct)
        => _generator.GenerateAsync(prompt, ct);
}
