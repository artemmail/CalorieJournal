using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FoodBot.Services;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class StatsController : ControllerBase
{
    private readonly StatsService _stats;

    public StatsController(StatsService stats)
    {
        _stats = stats;
    }

    private long GetChatId() =>
        long.TryParse(User.FindFirstValue("chat_id"), out var id) ? id : throw new UnauthorizedAccessException();

    [HttpGet("summary")]
    public Task<StatsSummary> Summary([FromQuery] int days = 1, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 30);
        return _stats.GetSummaryAsync(GetChatId(), days, ct);
    }

    [HttpGet("daily")]
    public Task<List<DailyTotals>> Daily([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        return _stats.GetDailyTotalsAsync(GetChatId(), from, to, ct);
    }
}
