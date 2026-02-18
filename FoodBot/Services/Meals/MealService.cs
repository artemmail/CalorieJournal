using System;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FoodBot.Data;
using FoodBot.Models;

namespace FoodBot.Services;

public sealed class MealService : IMealService
{
    private readonly IMealRepository _repo;
    private readonly IMealNotifier _notifier;

    public MealService(IMealRepository repo, IMealNotifier notifier)
    {
        _repo = repo;
        _notifier = notifier;
    }

    public async Task<MealListResult> ListAsync(long chatId, int limit, int offset, CancellationToken ct)
    {
        static string? ComputeIngredientsJson1Safe(string? productsJson)
        {
            if (string.IsNullOrWhiteSpace(productsJson))
                return null;

            if (productsJson.Length > 1_000_000)
                return null;

            try
            {
                var products = ProductJsonHelper.DeserializeProducts(productsJson);
                if (products == null)
                    return null;

                var names = products
                    .Select(p => p?.name?.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(128)
                    .ToArray();

                return names.Length > 0
                    ? JsonSerializer.Serialize(names)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        static string[] ParseIngredientsOrEmpty(string? ingredientsJson)
        {
            if (string.IsNullOrWhiteSpace(ingredientsJson))
                return Array.Empty<string>();

            try
            {
                return JsonSerializer.Deserialize<string[]>(ingredientsJson!)
                       ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        static string[] BuildIngredientsArraySafe(string? productsJson, string? fallbackIngredientsJson)
        {
            var computed = ComputeIngredientsJson1Safe(productsJson);
            if (!string.IsNullOrWhiteSpace(computed))
                return ParseIngredientsOrEmpty(computed);

            return ParseIngredientsOrEmpty(fallbackIngredientsJson);
        }

        var mealsQuery = _repo.Meals
            .AsNoTracking()
            .Where(m => m.ChatId == chatId);

        var pendingMealsQuery = _repo.PendingMeals
            .AsNoTracking()
            .Where(p => p.ChatId == chatId);

        var mealsCount = await mealsQuery.CountAsync(ct);
        var pendingCount = await pendingMealsQuery.CountAsync(ct);
        var total = mealsCount + pendingCount;

        var pendingIdsInt = await _repo.PendingClarifies
            .AsNoTracking()
            .Where(p => p.ChatId == chatId)
            .Select(p => p.MealId)
            .ToListAsync(ct);

        var pendingSet = pendingIdsInt.Count > 0
            ? pendingIdsInt.ToHashSet()
            : new HashSet<int>();

        var mealRows = mealsQuery
            .Select(m => new
            {
                IsPendingMeal = false,
                IsPhotoPending = false,
                Id = m.Id,
                PendingRequestId = (int?)null,
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
            });

        var pendingRows = pendingMealsQuery
            .Select(p => new
            {
                IsPendingMeal = true,
                IsPhotoPending = p.Description == null,
                Id = -p.Id,
                PendingRequestId = (int?)p.Id,
                CreatedAtUtc = p.DesiredMealTimeUtc ?? p.CreatedAtUtc,
                DishName = p.Description,
                WeightG = (decimal?)null,
                CaloriesKcal = (decimal?)null,
                ProteinsG = (decimal?)null,
                FatsG = (decimal?)null,
                CarbsG = (decimal?)null,
                IngredientsJson = (string?)null,
                ProductsJson = (string?)null,
                HasImage = false
            });

        var rows = await mealRows
            .Concat(pendingRows)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.IsPendingMeal)
            .ThenByDescending(x => x.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            if (r.IsPendingMeal)
            {
                var pendingTitle = string.IsNullOrWhiteSpace(r.DishName)
                    ? (r.IsPhotoPending ? "Фото обрабатывается" : "Запрос обрабатывается")
                    : r.DishName;

                return new MealListItem(
                    r.Id,
                    r.CreatedAtUtc,
                    pendingTitle,
                    null,
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<ProductInfo>(),
                    false,
                    true,
                    true,
                    r.PendingRequestId,
                    null
                );
            }

            var updateQueued = pendingSet.Contains(r.Id);
            return new MealListItem(
                r.Id,
                r.CreatedAtUtc,
                r.DishName,
                r.WeightG,
                r.CaloriesKcal,
                r.ProteinsG,
                r.FatsG,
                r.CarbsG,
                BuildIngredientsArraySafe(r.ProductsJson, r.IngredientsJson),
                ProductJsonHelper.DeserializeProducts(r.ProductsJson),
                r.HasImage,
                updateQueued,
                updateQueued,
                null,
                null
            );
        }).ToList();

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

        var ingredients1 = products.Select(x => x.name).ToArray();

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
            ingredients1,
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

    public Task QueueImageAsync(long chatId, byte[] bytes, string fileMime, string? note, DateTimeOffset? desiredTime, CancellationToken ct)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var desired = (desiredTime ?? utcNow).ToUniversalTime();
        var pending = new PendingMeal
        {
            ChatId = chatId,
            CreatedAtUtc = utcNow,
            FileMime = fileMime,
            ImageBytes = bytes,
            Attempts = 0,
            DesiredMealTimeUtc = desired,
            ClarifyNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
        return _repo.QueuePendingMealAsync(pending, ct);
    }

    public Task QueueTextAsync(long chatId, string description, bool generateImage, DateTimeOffset? desiredTime, CancellationToken ct)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var desired = (desiredTime ?? utcNow).ToUniversalTime();
        var pending = new PendingMeal
        {
            ChatId = chatId,
            CreatedAtUtc = utcNow,
            Description = description,
            GenerateImage = generateImage,
            Attempts = 0,
            DesiredMealTimeUtc = desired
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

            var item = m.ToListItem();
            await _notifier.MealUpdated(chatId, item);
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
