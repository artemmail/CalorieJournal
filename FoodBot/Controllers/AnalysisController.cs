using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;
using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/analysis")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class AnalysisController : ControllerBase
{
    private readonly DietAnalysisService _service;
    private readonly BotDbContext _db;
    private readonly AnalysisDocxService _docxService;

    public AnalysisController(DietAnalysisService service, BotDbContext db, AnalysisDocxService docxService)
    {
        _service = service;
        _db = db;
        _docxService = docxService;
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
            period = ToClientPeriod(x.Period),
            createdAtUtc = x.CreatedAtUtc,
            isProcessing = x.IsProcessing,
            checksum = x.CaloriesChecksum,
            hasMarkdown = x.Markdown != null
        }));
    }

    // Создать новый (ставим в очередь, с проверкой checksum)
    public sealed record CreateReportRequest(AnalysisPeriod period, DateOnly? date = null);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        (string status, AnalysisReport1 report) result;

        if (req.date.HasValue)
        {
            if (req.period != AnalysisPeriod.Day)
                return BadRequest(new { error = "date_not_supported_for_period" });

            result = await _service.StartReportAsync(chatId, req.period, ct, req.date);
        }
        else
        {
            result = await _service.StartReportAsync(chatId, req.period, ct);
        }

        var (status, report) = result;
        return Ok(new
        {
            status,
            id = report.Id,
            name = report.Name,
            period = ToClientPeriod(report.Period)
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
            period = ToClientPeriod(r.Period)
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

    [HttpGet("{id:long}/docx")]
    public async Task<IActionResult> DownloadDocx([FromRoute] long id, CancellationToken ct)
    {
        var chatId = User.GetChatId();
        var report = await _service.GetReportAsync(chatId, id, ct);
        if (report == null) return NotFound();
        if (report.IsProcessing || string.IsNullOrEmpty(report.Markdown)) return BadRequest(new { error = "report_not_ready" });
        if (string.IsNullOrWhiteSpace(report.RequestJson)) return BadRequest(new { error = "payload_missing" });

        var (stream, fileName) = await _docxService.BuildAsync(report, ct);
        return File(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
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
        return Ok(new { status = "ok", markdown, period = ToClientPeriod(period) });
    }

    private static string ToClientPeriod(AnalysisPeriod period)
        => JsonNamingPolicy.CamelCase.ConvertName(period.ToString());
}
