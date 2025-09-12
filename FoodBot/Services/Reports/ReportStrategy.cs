using System;
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

    public async Task<object> LoadDataAsync(long chatId, CancellationToken ct)
        => await _loader.LoadAsync(chatId, Period, ct);

    public string BuildPrompt(object data)
    {
        if (data is not ReportData<ReportPayload> typed)
            throw new ArgumentException("Invalid report data", nameof(data));
        return _promptBuilder.Build(typed);
    }

    public Task<string> GenerateAsync(object data, CancellationToken ct)
    {
        if (data is not string prompt)
            throw new ArgumentException("Prompt must be a string", nameof(data));
        return _generator.GenerateAsync(prompt, ct);
    }
}
