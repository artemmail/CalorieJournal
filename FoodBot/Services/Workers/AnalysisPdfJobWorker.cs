using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using System.IO;

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

                var job = await db.AnalysisPdfJobs
                    .Where(j => j.Status == AnalysisPdfJobStatus.Queued)
                    .OrderBy(j => j.CreatedAtUtc)
                    .FirstOrDefaultAsync(stoppingToken);
                if (job == null)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                job.Status = AnalysisPdfJobStatus.InProgress;
                await db.SaveChangesAsync(stoppingToken);

                try
                {
                    var report = await db.AnalysisReports2.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.Id == job.ReportId && r.ChatId == job.ChatId, stoppingToken);
                    if (report == null || string.IsNullOrEmpty(report.Markdown))
                        throw new InvalidOperationException($"Report {job.ReportId} not found or empty");

                    var (stream, fileName) = await pdf.BuildAsync(report.Name ?? $"report_{job.ReportId}", report.Markdown, stoppingToken);
                    stream.Position = 0;

                    var baseDir = Path.Combine(AppContext.BaseDirectory, "analysis-pdf-jobs");
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
                            await sender.SendAsync(job.ChatId, sendStream, fileName, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Failed to send analysis PDF job {Id}", job.Id);
                        }
                    }
                    else
                    {
                        _log.LogWarning("ITelegramBotClient not available for analysis PDF job {Id}", job.Id);
                    }

                    job.Status = AnalysisPdfJobStatus.Done;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "AnalysisPdfJob {Id} failed", job.Id);
                    job.Status = AnalysisPdfJobStatus.Error;
                    job.FinishedAtUtc = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
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
