using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/analysis")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class AnalysisController : ControllerBase
{
    private readonly DietAnalysisService _service;
    private readonly BotDbContext _db;

    public AnalysisController(DietAnalysisService service, BotDbContext db)
    {
        _service = service;
        _db = db;
    }

    // История
    [HttpGet("reports")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var list = await _service.ListReportsAsync(chatId, ct);
        return Ok(list.Select(x => new {
            id = x.Id,
            name = x.Name,
            period = x.Period.ToString().ToLower(),
            createdAtUtc = x.CreatedAtUtc,
            isProcessing = x.IsProcessing,
            checksum = x.CaloriesChecksum,
            hasMarkdown = x.Markdown != null
        }));
    }

    // Создать новый (ставим в очередь, с проверкой checksum)
    public sealed record CreateReportRequest(AnalysisPeriod period);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var (status, report) = await _service.StartReportAsync(chatId, req.period, ct);
        return Ok(new
        {
            status,
            id = report.Id,
            name = report.Name,
            period = report.Period.ToString().ToLower()
        });
    }

    // Получить готовый
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById([FromRoute] long id, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var r = await _service.GetReportAsync(chatId, id, ct);
        if (r == null) return NotFound();

        if (r.IsProcessing) return Accepted(new { status = "processing" });

        return Ok(new
        {
            status = "ok",
            markdown = r.Markdown,
            createdAtUtc = r.CreatedAtUtc,
            checksum = r.CaloriesChecksum,
            name = r.Name,
            period = r.Period.ToString().ToLower()
        });
    }

    [HttpPost("{id:long}/pdf")]
    public async Task<IActionResult> GetPdf([FromRoute] long id, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var r = await _service.GetReportAsync(chatId, id, ct);
        if (r == null) return NotFound();
        if (r.IsProcessing || string.IsNullOrEmpty(r.Markdown)) return BadRequest();

        var job = new AnalysisPdfJob
        {
            ChatId = chatId,
            ReportId = id,
            Status = AnalysisPdfJobStatus.Queued,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.AnalysisPdfJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return Accepted(new { id = job.Id });
    }

    [HttpGet("pdf-jobs/{id:long}")]
    public async Task<IActionResult> GetPdfJob([FromRoute] long id, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var job = await _db.AnalysisPdfJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ChatId == chatId, ct);
        if (job == null) return NotFound();

        if (job.Status == AnalysisPdfJobStatus.Done &&
            !string.IsNullOrEmpty(job.FilePath) && System.IO.File.Exists(job.FilePath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(job.FilePath, ct);
            var fn = Path.GetFileName(job.FilePath);
            return File(bytes, "application/pdf", fn);
        }

        if (job.Status == AnalysisPdfJobStatus.Error)
            return Ok(new { status = "error" });

        return Accepted(new { status = job.Status.ToString().ToLowerInvariant() });
    }

    // --- старые маршруты оставлены для обратной совместимости с прежним UI ---

    [HttpGet("day")]
    public async Task<IActionResult> GetDay(CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var report = await _service.GetDailyAsync(chatId, ct);
        if (report.IsProcessing)
            return Accepted(new { status = "processing" });
        return Ok(new { status = "ok", markdown = report.Markdown, createdAtUtc = report.CreatedAtUtc });
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AnalysisPeriod period, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        if (period == AnalysisPeriod.Day)
        {
            var report = await _service.GetDailyAsync(chatId, ct);
            if (report.IsProcessing)
                return Accepted(new { status = "processing" });
            return Ok(new { status = "ok", markdown = report.Markdown, createdAtUtc = report.CreatedAtUtc });
        }

        var markdown = await _service.GetPlanAsync(chatId, period, ct);
        return Ok(new { status = "ok", markdown, period = period.ToString().ToLower() });
    }
}
