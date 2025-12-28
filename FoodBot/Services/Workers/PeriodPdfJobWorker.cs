using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
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
                var docx = scope.ServiceProvider.GetRequiredService<DocxReportService>();

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
                    (MemoryStream stream, string fileName) result = job.Format switch
                    {
                        PeriodReportFormat.Docx => await docx.BuildAsync(job.AppUserId, job.From, job.To, stoppingToken),
                        _ => await pdf.BuildAsync(job.AppUserId, job.From, job.To, stoppingToken)
                    };
                    var stream = result.stream;
                    var fileName = result.fileName;
                    stream.Position = 0;

                    var baseDir = Path.Combine(AppContext.BaseDirectory, "report-jobs");
                    Directory.CreateDirectory(baseDir);
                    var filePath = Path.Combine(baseDir, $"{job.Id}_{fileName}");
                    await using (var fs = File.Create(filePath))
                    {
                        await stream.CopyToAsync(fs, stoppingToken);
                    }

                    job.FilePath = filePath;

                    var bot = scope.ServiceProvider.GetService<ITelegramBotClient>();
                    if (bot != null)
                    {
                        var sender = new TelegramPdfSender(bot);
                        try
                        {
                            await using var sendStream = File.OpenRead(filePath);
                            await sender.SendAsync(job.AppUserId, sendStream, fileName, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Failed to send period report job {Id}", job.Id);
                        }
                    }
                    else
                    {
                        _log.LogWarning("ITelegramBotClient not available for period report job {Id}", job.Id);
                    }

                    job.Status = PeriodPdfJobStatus.Done;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Period report job {Id} failed", job.Id);
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

