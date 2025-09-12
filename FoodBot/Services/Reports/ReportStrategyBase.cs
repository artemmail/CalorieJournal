using System;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;

namespace FoodBot.Services.Reports;

public abstract class ReportStrategyBase : IReportStrategy
{
    private readonly ReportDataLoader _loader;
    private readonly IPromptBuilder _promptBuilder;
    private readonly AnalysisGenerator _generator;

    protected ReportStrategyBase(
        AnalysisPeriod period,
        ReportDataLoader loader,
        IPromptBuilder promptBuilder,
        AnalysisGenerator generator)
    {
        Period = period;
        _loader = loader;
        _promptBuilder = promptBuilder;
        _generator = generator;
    }

    public AnalysisPeriod Period { get; }

    public Task<object> LoadDataAsync(long chatId, CancellationToken ct)
        => _loader.LoadAsync(chatId, Period, ct);

    public string BuildPrompt(object data) => _promptBuilder.Build(data);

    public Task<string> GenerateAsync(object data, CancellationToken ct)
    {
        if (data is not string prompt)
            throw new ArgumentException("Prompt must be a string", nameof(data));
        return _generator.GenerateAsync(prompt, ct);
    }
}
