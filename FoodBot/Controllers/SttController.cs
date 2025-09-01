using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/stt")]
[Authorize(AuthenticationSchemes = "Bearer")]
public sealed class SttController : ControllerBase
{
    private readonly SpeechToTextService _stt;

    public SttController(SpeechToTextService stt)
    {
        _stt = stt;
    }

    [HttpPost]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Transcribe([FromForm] IFormFile audio, [FromForm] string? language, CancellationToken ct)
    {
        if (audio == null || audio.Length == 0) return BadRequest("audio required");

        await using var ms = new MemoryStream();
        await audio.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var text = await _stt.TranscribeAsync(bytes, language ?? "ru", audio.FileName ?? "audio", audio.ContentType ?? "application/octet-stream", ct);
        if (string.IsNullOrWhiteSpace(text)) return BadRequest("stt_failed");

        return Ok(new { text });
    }
}

