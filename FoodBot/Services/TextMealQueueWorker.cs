using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class TextMealQueueWorker : BackgroundService
{
    private readonly ILogger<TextMealQueueWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;

    public TextMealQueueWorker(ILogger<TextMealQueueWorker> log, IServiceScopeFactory scopeFactory)
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
                var images = scope.ServiceProvider.GetRequiredService<MealImageService>();

                var next = await db.PendingMeals
                    .Where(x => x.Description != null)
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (next == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                try
                {
                    var conv = await nutrition.AnalyzeTextAsync(next.Description!, stoppingToken);


                    var (imgBytes, imgMime) = next.GenerateImage
                        ? await images.GenerateAsync(conv?.Result?.dish ?? next.Description!, stoppingToken)
                        : images.GeneratePlaceholder(conv?.Result?.dish ?? next.Description!);
                    var result = conv?.Result;
                    var entry = new MealEntry
                    {
                        ChatId = next.ChatId,
                        UserId = 0,
                        Username = "app",
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        FileId = string.Empty,
                        FileMime = imgMime,
                        ImageBytes = imgBytes,
                        DishName = result?.dish ?? next.Description,
                        IngredientsJson = result != null ? System.Text.Json.JsonSerializer.Serialize(result.ingredients) : "[]",
                        ProductsJson = conv != null ? ProductJsonHelper.BuildProductsJson(conv.CalcPlanJson) : "[]",
                        ProteinsG = result?.proteins_g ?? 0,
                        FatsG = result?.fats_g ?? 0,
                        CarbsG = result?.carbs_g ?? 0,
                        CaloriesKcal = result?.calories_kcal ?? 0,
                        Confidence = result?.confidence ?? 0,
                        WeightG = result?.weight_g ?? 0,
                        Model = "app",
                        Step1Json = conv != null ? System.Text.Json.JsonSerializer.Serialize(conv.Step1) : null,
                        ReasoningPrompt = conv?.ReasoningPrompt,
                        ClarifyNote = next.Description
                    };
                    db.Meals.Add(entry);
                    db.PendingMeals.Remove(next);
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // Another worker could have processed this pending meal earlier.
                    _log.LogWarning(ex, "Pending meal {Id} was already handled", next.Id);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "TextMealQueueWorker processing failed for {Id}", next.Id);
                    // The entity might still be marked as deleted after a failed save.
                    // Reset the state so we can update retry attempts.
                    if (db.Entry(next).State == EntityState.Deleted)
                        db.Entry(next).State = EntityState.Unchanged;
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
                _log.LogError(ex, "TextMealQueueWorker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
