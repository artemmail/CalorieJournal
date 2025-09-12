using FoodBot.Models;

namespace FoodBot.Services;

public interface IReportStrategy
{
    AnalysisPeriod Period { get; }
    IPromptBuilder PromptBuilder { get; }
}
