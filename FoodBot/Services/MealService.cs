using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services;

public sealed class MealService : IMealService
{
    private readonly IMealRepository _repo;

    public MealService(IMealRepository repo)
    {
        _repo = repo;
    }

    public async Task<MealListResult> ListAsync(long chatId, int limit, int offset, CancellationToken ct)
    {
        var baseQuery = _repo.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.CreatedAtUtc);

        var total = await baseQuery.CountAsync(ct);

        var pendingIds = await _repo.PendingClarifies.AsNoTracking()
            .Where(p => p.ChatId == chatId)
            .Select(p => p.MealId)
            .ToListAsync(ct);

        var rows = await baseQuery
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
            .ToListAsync(ct);

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
            r.HasImage,
            pendingIds.Contains(r.Id)
        )).ToList();

        return new MealListResult(total, offset, limit, items);
    }

    public async Task<MealDetails?> GetDetailsAsync(long chatId, int id, CancellationToken ct)
    {
        var m = await _repo.Meals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId && x.Id == id, ct);
        if (m == null) return null;

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
            m.ClarifyNote,
            step1,
            m.ReasoningPrompt,
            m.ImageBytes != null && m.ImageBytes.Length > 0
        );
        return details;
    }

    public async Task<(byte[] bytes, string mime)?> GetImageAsync(long chatId, int id, CancellationToken ct)
    {
        var m = await _repo.Meals.AsNoTracking()
            .Where(x => x.ChatId == chatId && x.Id == id)
            .Select(x => new { x.ImageBytes, x.FileMime })
            .FirstOrDefaultAsync(ct);
        if (m == null || m.ImageBytes == null || m.ImageBytes.Length == 0)
            return null;
        var mime = string.IsNullOrWhiteSpace(m.FileMime) ? "image/jpeg" : m.FileMime!;
        return new(m.ImageBytes, mime);
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

    public Task QueueTextAsync(long chatId, string description, bool generateImage, CancellationToken ct)
    {
        var pending = new PendingMeal
        {
            ChatId = chatId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Description = description,
            GenerateImage = generateImage,
            Attempts = 0
        };
        return _repo.QueuePendingMealAsync(pending, ct);
    }

    public async Task<ClarifyTextResult?> ClarifyTextAsync(long chatId, int id, string? note, DateTimeOffset? time, CancellationToken ct)
    {
        var m = await _repo.Meals.FirstOrDefaultAsync(x => x.ChatId == chatId && x.Id == id, ct);
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
                m.ClarifyNote,
                step1,
                m.ReasoningPrompt,
                m.ImageBytes != null && m.ImageBytes.Length > 0
            );
            return new ClarifyTextResult(false, details);
        }

        m.ClarifyNote = note;
        await _repo.SaveChangesAsync(ct);

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
        var m = await _repo.Meals.FirstOrDefaultAsync(x => x.ChatId == chatId && x.Id == id, ct);
        if (m == null) return false;
        await _repo.RemoveMealAsync(m, ct);
        return true;
    }
}
