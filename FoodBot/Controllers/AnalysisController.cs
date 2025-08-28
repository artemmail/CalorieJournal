using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var chatId = GetChatId();
        var report = await _service.GetOrGenerateAsync(chatId, ct);
        if (report.IsProcessing)
            return Accepted(new { status = "processing" });
        return Ok(new { status = "ok", markdown = report.Markdown, createdAtUtc = report.CreatedAtUtc });
    }
}
