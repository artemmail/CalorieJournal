using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FoodBot.Data;

namespace FoodBot.Services;

public sealed class MealService : IMealService
{
    private readonly IMealRepository _repo;

    public MealService(IMealRepository repo)
    {
        _repo = repo;
    }

    public Task<MealListResult> ListAsync(long chatId, int limit, int offset, CancellationToken ct)
    {
        var baseQuery = _repo.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.CreatedAtUtc);

        var total = baseQuery.Count();

        var rows = baseQuery
            .Skip(offset)
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.CreatedAtUtc,
                m.DishName,
                m.WeightG,
                m.CaloriesKcal,
                m.ProteinsG,
                m.FatsG,
                m.CarbsG,
                m.IngredientsJson,
                m.ProductsJson,
                HasImage = m.ImageBytes != null && m.ImageBytes.Length > 0
            })
            .ToList();

        var items = rows.Select(r => new MealListItem(
            r.Id,
            r.CreatedAtUtc,
            r.DishName,
            r.WeightG,
            r.CaloriesKcal,
            r.ProteinsG,
            r.FatsG,
            r.CarbsG,
            string.IsNullOrWhiteSpace(r.IngredientsJson)
                ? Array.Empty<string>()
                : (JsonSerializer.Deserialize<string[]>(r.IngredientsJson!) ?? Array.Empty<string>()),
            ProductJsonHelper.DeserializeProducts(r.ProductsJson),
            r.HasImage
        )).ToList();

        return Task.FromResult(new MealListResult(total, offset, limit, items));
    }

    public Task<MealDetails?> GetDetailsAsync(long chatId, int id, CancellationToken ct)
    {
        var m = _repo.Meals.AsNoTracking()
            .FirstOrDefault(x => x.ChatId == chatId && x.Id == id);
        if (m == null) return Task.FromResult<MealDetails?>(null);

        var ingredients = string.IsNullOrWhiteSpace(m.IngredientsJson)
            ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<string[]>(m.IngredientsJson!) ?? Array.Empty<string>());

        Step1Snapshot? step1 = null;
        if (!string.IsNullOrWhiteSpace(m.Step1Json))
        {
            try
            {
                step1 = JsonSerializer.Deserialize<Step1Snapshot>(m.Step1Json!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        var products = ProductJsonHelper.DeserializeProducts(m.ProductsJson);

        var details = new MealDetails(
            m.Id,
            m.CreatedAtUtc,
            m.DishName,
            m.WeightG,
            m.CaloriesKcal,
            m.ProteinsG,
            m.FatsG,
            m.CarbsG,
            m.Confidence,
            ingredients,
            products,
            step1,
            m.ReasoningPrompt,
            m.ImageBytes != null && m.ImageBytes.Length > 0
        );
        return Task.FromResult<MealDetails?>(details);
    }

    public Task<(byte[] bytes, string mime)?> GetImageAsync(long chatId, int id, CancellationToken ct)
    {
        var m = _repo.Meals.AsNoTracking()
            .Where(x => x.ChatId == chatId && x.Id == id)
            .Select(x => new { x.ImageBytes, x.FileMime })
            .FirstOrDefault();
        if (m == null || m.ImageBytes == null || m.ImageBytes.Length == 0)
            return Task.FromResult<(byte[] bytes, string mime)?>(null);
        var mime = string.IsNullOrWhiteSpace(m.FileMime) ? "image/jpeg" : m.FileMime!;
        return Task.FromResult<(byte[] bytes, string mime)?>(new(m.ImageBytes, mime));
    }

    public Task QueueImageAsync(long chatId, byte[] bytes, string fileMime, CancellationToken ct)
    {
        var pending = new PendingMeal
        {
            ChatId = chatId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FileMime = fileMime,
            ImageBytes = bytes,
            Attempts = 0
        };
        return _repo.QueuePendingMealAsync(pending, ct);
    }

    public async Task<ClarifyTextResult?> ClarifyTextAsync(long chatId, int id, string? note, DateTimeOffset? time, CancellationToken ct)
    {
        var m = _repo.Meals.FirstOrDefault(x => x.ChatId == chatId && x.Id == id);
        if (m == null) return null;

        if (string.IsNullOrWhiteSpace(note))
        {
            if (time.HasValue)
            {
                m.CreatedAtUtc = time.Value.ToUniversalTime();
                await _repo.SaveChangesAsync(ct);
            }

            var ingredients = string.IsNullOrWhiteSpace(m.IngredientsJson)
                ? Array.Empty<string>()
                : (JsonSerializer.Deserialize<string[]>(m.IngredientsJson!) ?? Array.Empty<string>());

            Step1Snapshot? step1 = null;
            if (!string.IsNullOrWhiteSpace(m.Step1Json))
            {
                try
                {
                    step1 = JsonSerializer.Deserialize<Step1Snapshot>(m.Step1Json!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { }
            }

            var products = ProductJsonHelper.DeserializeProducts(m.ProductsJson);
            var details = new MealDetails(
                m.Id,
                m.CreatedAtUtc,
                m.DishName,
                m.WeightG,
                m.CaloriesKcal,
                m.ProteinsG,
                m.FatsG,
                m.CarbsG,
                m.Confidence,
                ingredients,
                products,
                step1,
                m.ReasoningPrompt,
                m.ImageBytes != null && m.ImageBytes.Length > 0
            );
            return new ClarifyTextResult(false, details);
        }

        var pending = new PendingClarify
        {
            ChatId = chatId,
            MealId = id,
            Note = note!,
            NewTime = time?.ToUniversalTime(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Attempts = 0
        };
        await _repo.QueuePendingClarifyAsync(pending, ct);
        return new ClarifyTextResult(true, null);
    }

    public async Task<bool> DeleteAsync(long chatId, int id, CancellationToken ct)
    {
        var m = _repo.Meals.FirstOrDefault(x => x.ChatId == chatId && x.Id == id);
        if (m == null) return false;
        await _repo.RemoveMealAsync(m, ct);
        return true;
    }
}
