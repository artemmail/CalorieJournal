using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;   // <-- важно
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodBot.Services;

public sealed class AnalysisQueueWorker : BackgroundService
{
    private readonly ILogger<AnalysisQueueWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;

    public AnalysisQueueWorker(ILogger<AnalysisQueueWorker> log, IServiceScopeFactory scopeFactory)
    {
        _log = log;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Очистка зависших/висящих при старте — в отдельном скоупе.
        // Если схема БД отстаёт (например, после деплоя на другой машине),
        // не валим весь хост: API должно продолжать работать.
        try
        {
            using var startupScope = _scopeFactory.CreateScope();
            var service = startupScope.ServiceProvider.GetRequiredService<DietAnalysisService>();
            await service.CleanupAllProcessingOnStartupAsync(stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // shutdown path
        }
        catch (System.Exception ex)
        {
            _log.LogError(ex, "AnalysisQueueWorker startup cleanup failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<DietAnalysisService>();

                // чистим зависшие > 10 минут
                await service.CleanupStaleAsync(stoppingToken);

                // берём следующую запись в обработке
                var next = await db.AnalysisReports2.AsNoTracking()
                    .Where(x => x.IsProcessing && x.Markdown == null)
                    .OrderBy(x => x.ProcessingStartedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (next == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                await service.GenerateForRecordAsync(next.Id, stoppingToken);
            }
            catch (TaskCanceledException) { /* нормальное завершение */ }
            catch (System.Exception ex)
            {
                _log.LogError(ex, "AnalysisQueueWorker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
