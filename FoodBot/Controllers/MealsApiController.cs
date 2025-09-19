using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Services;
using FoodBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace FoodBot.Controllers
{
    [ApiController]
    [Route("api/meals")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public sealed class MealsController : ControllerBase
    {
        private readonly IMealService _meals;
        private readonly TelegramReportService _report;
        private readonly SpeechToTextService _stt;

        public MealsController(IMealService meals, TelegramReportService report, SpeechToTextService stt)
        {
            _meals = meals;
            _report = report;
            _stt = stt;
        }

        // ---------- История (список) ----------
        // GET /api/meals?limit=30&offset=0
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int limit = 30, [FromQuery] int offset = 0, CancellationToken ct = default)
        {
            var chatId = User.GetChatId();
            limit = Math.Clamp(limit, 1, 100);
            offset = Math.Max(0, offset);

            var result = await _meals.ListAsync(chatId, limit, offset, ct);
            return Ok(result);
        }

        // ---------- Детали блюда ----------
        // GET /api/meals/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details([FromRoute] int id, CancellationToken ct)
        {
            var chatId = User.GetChatId();

            var m = await _meals.GetDetailsAsync(chatId, id, ct);
            if (m == null) return NotFound();
            return Ok(new
            {
                m.Id,
                m.CreatedAtUtc,
                m.DishName,
                m.WeightG,
                m.CaloriesKcal,
                m.ProteinsG,
                m.FatsG,
                m.CarbsG,
                m.Confidence,
                Ingredients = m.Ingredients,
                Products = m.Products,
                ClarifyNote = m.ClarifyNote,
                Step1 = m.Step1,
                ReasoningPrompt = m.ReasoningPrompt,
                HasImage = m.HasImage
            });
        }

        // ---------- Фото блюда ----------
        // GET /api/meals/{id}/image
        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> Image([FromRoute] int id, CancellationToken ct)
        {
            var chatId = User.GetChatId();
            var img = await _meals.GetImageAsync(chatId, id, ct);
            if (img == null) return NotFound();
            return File(img.Value.bytes, img.Value.mime);
        }

        // ---------- Загрузка фото ----------
        // POST /api/meals/upload  (multipart/form-data: image)
        [HttpPost("upload")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> Upload(
            [FromForm] IFormFile image,
            [FromForm] string? note,
            [FromForm] string? time,
            CancellationToken ct)
        {
            if (image == null || image.Length == 0) return BadRequest("image required");

            await using var ms = new MemoryStream();
            await image.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var chatId = User.GetChatId();
            var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            DateTimeOffset? desiredTime = null;
            if (!string.IsNullOrWhiteSpace(time) && DateTimeOffset.TryParse(time, out var parsedTime))
            {
                desiredTime = parsedTime;
            }

            await _meals.QueueImageAsync(chatId, bytes, image.ContentType ?? "image/jpeg", trimmedNote, desiredTime, ct);
            return Ok(new { queued = true });
        }

        // ---------- Добавление по тексту/голосу ----------
        public sealed record AddTextReq(string text, bool generateImage, DateTimeOffset? time);

        // POST /api/meals/add-text
        [HttpPost("add-text")]
        public async Task<IActionResult> AddText([FromBody] AddTextReq req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.text)) return BadRequest("text required");
            var chatId = User.GetChatId();
            await _meals.QueueTextAsync(chatId, req.text, req.generateImage, req.time, ct);
            return Ok(new { queued = true });
        }

        // POST /api/meals/add-voice  (multipart/form-data: audio; optional language=ru)
        [HttpPost("add-voice")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> AddVoice([FromForm] IFormFile audio, [FromForm] string? language, [FromForm] bool generateImage, CancellationToken ct)
        {
            if (audio == null || audio.Length == 0) return BadRequest("audio required");
            var chatId = User.GetChatId();
            await using var ms = new MemoryStream();
            await audio.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            var text = await _stt.TranscribeAsync(bytes, language ?? "ru", audio.FileName ?? "audio", audio.ContentType ?? "application/octet-stream", ct);
            if (string.IsNullOrWhiteSpace(text)) return BadRequest("stt_failed");
            await _meals.QueueTextAsync(chatId, text, generateImage, null, ct);
            return Ok(new { queued = true });
        }

        // ---------- Уточнение: текст ----------
        public sealed record ClarifyTextReq(string? note, DateTimeOffset? time);

        // POST /api/meals/{id}/clarify-text
        [HttpPost("{id:int}/clarify-text")]
        public async Task<IActionResult> ClarifyText([FromRoute] int id, [FromBody] ClarifyTextReq req, CancellationToken ct)
        {
            var chatId = User.GetChatId();
            var result = await _meals.ClarifyTextAsync(chatId, id, req.note, req.time, ct);
            if (result == null) return NotFound();
            if (result.Queued) return Ok(new { queued = true });
            var d = result.Details!;
            return Ok(new
            {
                d.Id,
                CreatedAtUtc = d.CreatedAtUtc,
                Result = new
                {
                    dish = d.DishName,
                    ingredients = d.Ingredients,
                    proteins_g = d.ProteinsG,
                    fats_g = d.FatsG,
                    carbs_g = d.CarbsG,
                    calories_kcal = d.CaloriesKcal,
                    weight_g = d.WeightG,
                    confidence = d.Confidence
                },
                Products = d.Products,
                ClarifyNote = d.ClarifyNote,
                Step1 = d.Step1,
                ReasoningPrompt = d.ReasoningPrompt,
                CalcPlanJson = string.Empty
            });
        }

        // ---------- Уточнение: голос ----------
        // POST /api/meals/{id}/clarify-voice  (multipart/form-data: audio ; optional: language=ru)
        [HttpPost("{id:int}/clarify-voice")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> ClarifyVoice([FromRoute] int id, [FromForm] IFormFile audio, [FromForm] string? language, CancellationToken ct)
        {
            var chatId = User.GetChatId();
            if (audio == null || audio.Length == 0) return BadRequest("audio required");

            await using var ms = new MemoryStream();
            await audio.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var text = await _stt.TranscribeAsync(bytes, language ?? "ru", audio.FileName ?? "audio", audio.ContentType ?? "application/octet-stream", ct);
            if (string.IsNullOrWhiteSpace(text)) return BadRequest("stt_failed");

            var result = await _meals.ClarifyTextAsync(chatId, id, text, null, ct);
            if (result == null) return NotFound();
            return Ok(new { queued = true });
        }

        // ---------- Excel отчёт ----------
        // GET /api/meals/report
        [HttpGet("report")]
        public async Task<IActionResult> Report(CancellationToken ct)
        {
            var chatId = User.GetChatId();
            var (stream, filename) = await _report.BuildUserReportAsync(chatId, ct);
            stream.Position = 0;
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        // ---------- Удаление блюда ----------
        // DELETE /api/meals/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
        {
            var chatId = User.GetChatId();
            var ok = await _meals.DeleteAsync(chatId, id, ct);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}
