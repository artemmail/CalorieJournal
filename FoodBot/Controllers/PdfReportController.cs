using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FoodBot.Services;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/report")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class PdfReportController : ControllerBase
{
    private readonly PdfReportService _reports;

    public PdfReportController(PdfReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
    {
        var (stream, fileName) = await _reports.BuildAsync(User.GetChatId(), from, to, ct);
        stream.Position = 0;
        return File(stream, "application/pdf", fileName);
    }
}

