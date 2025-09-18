using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services;

public interface IMealService
{
    Task<MealListResult> ListAsync(long chatId, int limit, int offset, CancellationToken ct);
    Task<MealDetails?> GetDetailsAsync(long chatId, int id, CancellationToken ct);
    Task<(byte[] bytes, string mime)?> GetImageAsync(long chatId, int id, CancellationToken ct);
    Task QueueImageAsync(long chatId, byte[] bytes, string fileMime, CancellationToken ct);
    Task QueueTextAsync(long chatId, string description, bool generateImage, DateTimeOffset? desiredTime, CancellationToken ct);
    Task<ClarifyTextResult?> ClarifyTextAsync(long chatId, int id, string? note, DateTimeOffset? time, CancellationToken ct);
    Task<bool> DeleteAsync(long chatId, int id, CancellationToken ct);
}

public sealed record MealListResult(int Total, int Offset, int Limit, List<MealListItem> Items);

public sealed record MealListItem
(
    int Id,
    DateTimeOffset CreatedAtUtc,
    string? DishName,
    decimal? WeightG,
    decimal? CaloriesKcal,
    decimal? ProteinsG,
    decimal? FatsG,
    decimal? CarbsG,
    string[] Ingredients,
    FoodBot.Models.ProductInfo[] Products,
    bool HasImage,
    bool UpdateQueued
);

public sealed record MealDetails
(
    int Id,
    DateTimeOffset CreatedAtUtc,
    string? DishName,
    decimal? WeightG,
    decimal? CaloriesKcal,
    decimal? ProteinsG,
    decimal? FatsG,
    decimal? CarbsG,
    decimal? Confidence,
    string[] Ingredients,
    FoodBot.Models.ProductInfo[] Products,
    string? ClarifyNote,
    Step1Snapshot? Step1,
    string? ReasoningPrompt,
    bool HasImage
);

public sealed record ClarifyTextResult(bool Queued, MealDetails? Details);
