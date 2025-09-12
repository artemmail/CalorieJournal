using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public sealed class ReportStrategy : IReportStrategy
{
    private readonly IReportDataLoader _loader;
    private readonly IPromptBuilder _promptBuilder;
    private readonly AnalysisGenerator _generator;

    public ReportStrategy(AnalysisPeriod period, IReportDataLoader loader, IPromptBuilder promptBuilder, AnalysisGenerator generator)
    {
        Period = period;
        _loader = loader;
        _promptBuilder = promptBuilder;
        _generator = generator;
    }

    public AnalysisPeriod Period { get; }

    public async Task<ReportData<ReportPayload>> LoadDataAsync(long chatId, CancellationToken ct)
        => await _loader.LoadAsync(chatId, Period, ct);

    public string BuildPrompt(ReportData<ReportPayload> data)
        => _promptBuilder.Build(data);

    public Task<string> GenerateAsync(string prompt, CancellationToken ct)
        => _generator.GenerateAsync(prompt, ct);
}
