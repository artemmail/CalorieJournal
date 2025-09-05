using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace FoodBot.Services;

public sealed class AnalysisPdfJobWorker : BackgroundService
{
    private readonly ILogger<AnalysisPdfJobWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;

    public AnalysisPdfJobWorker(ILogger<AnalysisPdfJobWorker> log, IServiceScopeFactory scopeFactory)
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
                var pdf = scope.ServiceProvider.GetRequiredService<AnalysisPdfService>();
                var bot = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

                var job = await db.AnalysisPdfJobs
                    .Where(x => x.Status == AnalysisPdfJobStatus.Queued)
                    .OrderBy(x => x.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (job == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                job.Status = AnalysisPdfJobStatus.Processing;
                await db.SaveChangesAsync(stoppingToken);

                var report = await db.AnalysisReports2
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == job.ReportId && r.ChatId == job.ChatId, stoppingToken);
                if (report == null || string.IsNullOrEmpty(report.Markdown))
                {
                    job.Status = AnalysisPdfJobStatus.Failed;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                    continue;
                }

                var (stream, fileName) = await pdf.BuildAsync(report.Name ?? $"report_{report.Id}", report.Markdown, stoppingToken);
                stream.Position = 0;

                var dir = Path.Combine(AppContext.BaseDirectory, "pdf-jobs");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{job.Id}_{fileName}");
                using (var fs = File.Create(path))
                {
                    await stream.CopyToAsync(fs, stoppingToken);
                }

                job.FilePath = path;
                job.Status = AnalysisPdfJobStatus.Done;
                job.FinishedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(stoppingToken);

                stream.Position = 0;
                await bot.SendDocument(job.ChatId, InputFile.FromStream(stream, fileName), cancellationToken: stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "AnalysisPdfJobWorker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
