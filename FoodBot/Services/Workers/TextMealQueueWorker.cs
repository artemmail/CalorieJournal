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
                var notifier = scope.ServiceProvider.GetRequiredService<IMealNotifier>();

                var nextText = await db.PendingMeals
                    .Where(x => x.Description != null)
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);
                var nextClar = await db.PendingClarifies
                    .Join(db.Meals,
                        c => new { c.AppUserId, c.MealId },
                        m => new { m.AppUserId, MealId = m.Id },
                        (c, m) => new { Clar = c, Meal = m })
                    .Where(x => x.Meal.SourceType == MealSourceType.Description)
                    .OrderBy(x => x.Clar.CreatedAtUtc)
                    .Select(x => x.Clar)
                    .FirstOrDefaultAsync(stoppingToken);

                if (nextText == null && nextClar == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                if (nextClar != null && (nextText == null || nextClar.CreatedAtUtc <= nextText.CreatedAtUtc))
                {
                    try
                    {
                        var meal = await db.Meals
                            .Where(m => m.AppUserId == nextClar.AppUserId && m.Id == nextClar.MealId)
                            .FirstOrDefaultAsync(stoppingToken);
                        if (meal == null)
                        {
                            db.PendingClarifies.Remove(nextClar);
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        var conv = await nutrition.AnalyzeTextAsync(nextClar.Note, stoppingToken);
                        var result = conv?.Result;
                        meal.DishName = result?.dish ?? nextClar.Note;
                        meal.IngredientsJson = result != null ? System.Text.Json.JsonSerializer.Serialize(result.ingredients) : "[]";
                        meal.ProductsJson = conv != null ? ProductJsonHelper.BuildProductsJson(conv.CalcPlanJson) : "[]";
                        meal.ProteinsG = result?.proteins_g ?? 0;
                        meal.FatsG = result?.fats_g ?? 0;
                        meal.CarbsG = result?.carbs_g ?? 0;
                        meal.CaloriesKcal = result?.calories_kcal ?? 0;
                        meal.Confidence = result?.confidence ?? 0;
                        meal.WeightG = result?.weight_g ?? 0;
                        meal.Step1Json = conv != null ? System.Text.Json.JsonSerializer.Serialize(conv.Step1) : null;
                        meal.ReasoningPrompt = conv?.ReasoningPrompt;
                        if (nextClar.NewTime.HasValue)
                            meal.CreatedAtUtc = nextClar.NewTime.Value.ToUniversalTime();
                        meal.ClarifyNote = nextClar.Note;
                        db.PendingClarifies.Remove(nextClar);
                        await db.SaveChangesAsync(stoppingToken);

                        await notifier.MealUpdated(meal.AppUserId, meal.ToListItem());
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "TextMealQueueWorker clarification failed for {Id}", nextClar.Id);
                        nextClar.Attempts++;
                        if (nextClar.Attempts >= 3)
                            db.PendingClarifies.Remove(nextClar);
                        await db.SaveChangesAsync(stoppingToken);
                        await Task.Delay(2000, stoppingToken);
                    }
                    continue;
                }

                var next = nextText!;

                try
                {
                    var conv = await nutrition.AnalyzeTextAsync(next.Description!, stoppingToken);


                    var (imgBytes, imgMime) = next.GenerateImage
                        ? await images.GenerateAsync(conv?.Result?.dish ?? next.Description!, stoppingToken)
                        : images.GeneratePlaceholder(conv?.Result?.dish ?? next.Description!);
                    var result = conv?.Result;
                    var desiredTime = (next.DesiredMealTimeUtc ?? DateTimeOffset.UtcNow);

                    var entry = new MealEntry
                    {
                        AppUserId = next.AppUserId,
                        UserId = 0,
                        Username = "app",
                        CreatedAtUtc = desiredTime,
                        SourceType = MealSourceType.Description,
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

                    await notifier.MealUpdated(entry.AppUserId, entry.ToListItem());
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
