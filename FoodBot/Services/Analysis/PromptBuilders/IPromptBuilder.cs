using FoodBot.Services.Reports;

namespace FoodBot.Services;

/// <summary>
/// Builds prompt payloads for analysis reports.
/// </summary>
public interface IPromptBuilder<TData>
{
    /// <summary>
    /// Build a prompt using previously loaded report data.
    /// </summary>
    string Build(ReportData<TData> report);

    /// <summary>Override model name or <c>null</c> for default.</summary>
    string? Model { get; }
}
