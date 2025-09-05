using FoodBot.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/report")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class PdfReportController : ControllerBase
{
    private readonly BotDbContext _db;

    public PdfReportController(BotDbContext db)
    {
        _db = db;
    }

    [HttpPost("pdf")]
    public async Task<IActionResult> Post(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
    {
        var job = new PeriodPdfJob
        {
            ChatId = User.GetChatId(),
            From = from,
            To = to,
            Status = PeriodPdfJobStatus.Queued,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.PeriodPdfJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return Accepted(new { id = job.Id });
    }

    [HttpGet("pdf-jobs/{id:long}")]
    public async Task<IActionResult> GetJob([FromRoute] long id, CancellationToken ct = default)
    {
        var chatId = User.GetChatId();
        var job = await _db.PeriodPdfJobs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ChatId == chatId, ct);
        if (job == null) return NotFound();

        if (job.Status == PeriodPdfJobStatus.Done &&
            !string.IsNullOrEmpty(job.FilePath) && System.IO.File.Exists(job.FilePath))
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(job.FilePath, ct);
            var fn = Path.GetFileName(job.FilePath);
            return File(bytes, "application/pdf", fn);
        }

        if (job.Status == PeriodPdfJobStatus.Error)
            return Ok(new { status = "error" });

        return Accepted(new { status = job.Status.ToString().ToLowerInvariant() });
    }
}

