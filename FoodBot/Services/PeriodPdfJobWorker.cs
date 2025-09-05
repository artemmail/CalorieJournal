using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using System.IO;

namespace FoodBot.Services;

public sealed class PeriodPdfJobWorker : BackgroundService
{
    private readonly ILogger<PeriodPdfJobWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;

    public PeriodPdfJobWorker(ILogger<PeriodPdfJobWorker> log, IServiceScopeFactory scopeFactory)
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
                var pdf = scope.ServiceProvider.GetRequiredService<PdfReportService>();
                var bot = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

                var job = await db.PeriodPdfJobs
                    .Where(j => j.Status == PeriodPdfJobStatus.Queued)
                    .OrderBy(j => j.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);
                if (job == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                job.Status = PeriodPdfJobStatus.InProgress;
                await db.SaveChangesAsync(stoppingToken);

                try
                {
                    var (stream, fileName) = await pdf.BuildAsync(job.ChatId, job.From, job.To, stoppingToken);
                    stream.Position = 0;

                    var baseDir = Path.Combine(AppContext.BaseDirectory, "pdf-jobs");
                    Directory.CreateDirectory(baseDir);
                    var filePath = Path.Combine(baseDir, $"{job.Id}_{fileName}");
                    await using (var fs = File.Create(filePath))
                    {
                        await stream.CopyToAsync(fs, stoppingToken);
                    }

                    await using (var sendStream = File.OpenRead(filePath))
                    {
                        await bot.SendDocument(job.ChatId, InputFile.FromStream(sendStream, fileName), cancellationToken: stoppingToken);
                    }

                    job.Status = PeriodPdfJobStatus.Done;
                    job.FilePath = filePath;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "PeriodPdfJob {Id} failed", job.Id);
                    job.Status = PeriodPdfJobStatus.Error;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "PeriodPdfJobWorker iteration failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}

