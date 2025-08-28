using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Models;
using FoodBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/analysis")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class AnalysisController : ControllerBase
{
    private readonly DietAnalysisService _service;

    public AnalysisController(DietAnalysisService service)
    {
        _service = service;
    }

    private long GetChatId() =>
        long.TryParse(User.FindFirstValue("chat_id"), out var id) ? id : throw new UnauthorizedAccessException();

    [HttpGet("day")]
    public async Task<IActionResult> GetDay(CancellationToken ct)
    {
        var chatId = GetChatId();
        var report = await _service.GetDailyAsync(chatId, ct);
        if (report.IsProcessing)
            return Accepted(new { status = "processing" });
        return Ok(new { status = "ok", markdown = report.Markdown, createdAtUtc = report.CreatedAtUtc });
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AnalysisPeriod period, CancellationToken ct)
    {
        var chatId = GetChatId();
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
