namespace FoodBot.Services;

public interface IPromptBuilder
{
    string Build(object data);
    string? Model { get; }
}
