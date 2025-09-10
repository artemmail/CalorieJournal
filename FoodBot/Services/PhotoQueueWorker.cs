using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class PhotoQueueWorker : BackgroundService
{
    private readonly ILogger<PhotoQueueWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;

    public PhotoQueueWorker(ILogger<PhotoQueueWorker> log, IServiceScopeFactory scopeFactory)
    {
        _log = log;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
                var nutrition = scope.ServiceProvider.GetRequiredService<NutritionService>();

                var nextPhoto = await db.PendingMeals
                    .Where(x => x.Description == null)
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);
                var nextClar = await db.PendingClarifies
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (nextPhoto == null && nextClar == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                if (nextClar != null && (nextPhoto == null || nextClar.CreatedAtUtc <= nextPhoto.CreatedAtUtc))
                {
                    try
                    {
                        var meal = await db.Meals.Where(m => m.ChatId == nextClar.ChatId && m.Id == nextClar.MealId)
                            .FirstOrDefaultAsync(stoppingToken);
                        if (meal == null)
                        {
                            db.PendingClarifies.Remove(nextClar);
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        NutritionConversation? conv2 = null;
                        if (!string.IsNullOrWhiteSpace(meal.Step1Json))
                        {
                            try
                            {
                                var step1 = JsonSerializer.Deserialize<Step1Snapshot>(
                                    meal.Step1Json!,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (step1 is not null)
                                    conv2 = await nutrition.ClarifyFromStep1Async(step1, nextClar.Note, stoppingToken);
                            }
                            catch { conv2 = null; }
                        }

                        if (conv2 is null && meal.ImageBytes is { Length: > 0 })
                        {
                            conv2 = await nutrition.AnalyzeWithNoteAsync(meal.ImageBytes, nextClar.Note, ct: stoppingToken);
                        }

                        if (conv2 != null)
                        {
                            meal.DishName = conv2.Result.dish;
                            meal.IngredientsJson = JsonSerializer.Serialize(conv2.Result.ingredients);
                            meal.ProductsJson = ProductJsonHelper.BuildProductsJson(conv2.CalcPlanJson);
                            meal.ProteinsG = conv2.Result.proteins_g;
                            meal.FatsG = conv2.Result.fats_g;
                            meal.CarbsG = conv2.Result.carbs_g;
                            meal.CaloriesKcal = conv2.Result.calories_kcal;
                            meal.Confidence = conv2.Result.confidence;
                            meal.WeightG = conv2.Result.weight_g;
                            meal.ReasoningPrompt = conv2.ReasoningPrompt;
                            if (nextClar.NewTime.HasValue)
                                meal.CreatedAtUtc = nextClar.NewTime.Value.ToUniversalTime();

                            db.PendingClarifies.Remove(nextClar);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                        else
                        {
                            nextClar.Attempts++;
                            if (nextClar.Attempts >= 3)
                                db.PendingClarifies.Remove(nextClar);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "PhotoQueueWorker clarification failed for {Id}", nextClar.Id);
                        nextClar.Attempts++;
                        if (nextClar.Attempts >= 3)
                            db.PendingClarifies.Remove(nextClar);
                        await db.SaveChangesAsync(stoppingToken);
                        await Task.Delay(2000, stoppingToken);
                    }
                    continue;
                }

                var next = nextPhoto!;

                try
                {
                    var conv = await nutrition.AnalyzeAsync(next.ImageBytes, ct: stoppingToken);
                    if (conv != null)
                    {
                        var entry = new MealEntry
                        {
                            ChatId = next.ChatId,
                            UserId = 0,
                            Username = "app",
                            CreatedAtUtc = DateTimeOffset.UtcNow,
                            FileId = string.Empty,
                            FileMime = next.FileMime,
                            ImageBytes = next.ImageBytes,
                            DishName = conv.Result.dish,
                            IngredientsJson = JsonSerializer.Serialize(conv.Result.ingredients),
                            ProductsJson = ProductJsonHelper.BuildProductsJson(conv.CalcPlanJson),
                            ProteinsG = conv.Result.proteins_g,
                            FatsG = conv.Result.fats_g,
                            CarbsG = conv.Result.carbs_g,
                            CaloriesKcal = conv.Result.calories_kcal,
                            Confidence = conv.Result.confidence,
                            WeightG = conv.Result.weight_g,
                            Model = "app",
                            Step1Json = JsonSerializer.Serialize(conv.Step1),
                            ReasoningPrompt = conv.ReasoningPrompt
                        };
                        db.Meals.Add(entry);
                        db.PendingMeals.Remove(next);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    else
                    {
                        next.Attempts++;
                        if (next.Attempts >= 3)
                            db.PendingMeals.Remove(next);
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "PhotoQueueWorker processing failed for {Id}", next.Id);
                    next.Attempts++;
                    if (next.Attempts >= 3)
                        db.PendingMeals.Remove(next);
                    await db.SaveChangesAsync(stoppingToken);
                    await Task.Delay(2000, stoppingToken);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "PhotoQueueWorker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
