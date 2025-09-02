using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
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

                var next = await db.PendingMeals
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (next == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

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
