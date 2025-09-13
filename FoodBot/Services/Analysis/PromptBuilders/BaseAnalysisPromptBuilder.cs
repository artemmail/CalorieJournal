using System.Text.Json;
using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Base helper for building analysis prompts with shared JSON payload structure.
/// Derived builders supply period specific instructions and extra inputs.
/// </summary>
public abstract class BaseAnalysisPromptBuilder<TData> : IPromptBuilder<TData>
{
    /// <summary>LLM model name or <c>null</c> to use default.</summary>
    public virtual string? Model => "o4-mini";

    /// <summary>Period specific instructions in Russian.</summary>
    protected abstract string BuildInstructions(ReportData<TData> report);

    /// <summary>Additional input items appended after the report JSON.</summary>
    /// <remarks>
    /// Builders may override to provide extra context, e.g. weekly tables.
    /// </remarks>
    protected virtual IEnumerable<object>? ExtraInputs(ReportData<TData> report) => null;

    /// <inheritdoc />
    public string Build(ReportData<TData> report)
    {
        var content = new List<object>
        {
            new { type = "input_text", text = BuildInstructions(report) },
            new { type = "input_text", text = JsonSerializer.Serialize(report.Data) }
        };

        var extras = ExtraInputs(report);
        if (extras != null)
        {
            content.AddRange(extras);
        }

        var reqObj = new
        {
            model = Model ?? "o4-mini",
            input = new object[]
            {
                new { role = "system", content = "You are a helpful dietologist." },
                new { role = "user", content }
            }
        };

        return JsonSerializer.Serialize(reqObj);
    }
}

