using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using FoodBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Step1Snapshot = FoodBot.Services.Step1Snapshot;
using NutritionConversation = FoodBot.Services.NutritionConversation;


namespace FoodBot.Controllers
{
    [ApiController]
    [Route("api/meals")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public sealed class MealsController : ControllerBase
    {
        private readonly BotDbContext _db;
        private readonly NutritionService _nutrition;
        private readonly TelegramReportService _report;
        private readonly SpeechToTextService _stt;

        public MealsController(BotDbContext db, NutritionService nutrition, TelegramReportService report, SpeechToTextService stt)
        {
            _db = db;
            _nutrition = nutrition;
            _report = report;
            _stt = stt;
        }

        private long GetChatId() =>
            long.TryParse(User.FindFirstValue("chat_id"), out var id) ? id : throw new UnauthorizedAccessException();

        // ---------- История (список) ----------
        // GET /api/meals?limit=30&offset=0
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int limit = 30, [FromQuery] int offset = 0, CancellationToken ct = default)
        {
            var chatId = GetChatId();
            limit = Math.Clamp(limit, 1, 100);
            offset = Math.Max(0, offset);

            var baseQuery = _db.Meals
                .AsNoTracking()
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAtUtc);

            var total = await baseQuery.CountAsync(ct);

            // 1) забираем «сырые» данные без JsonSerializer в выражении
            var rows = await baseQuery
                .Skip(offset)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.CreatedAtUtc,
                    m.DishName,
                    m.WeightG,
                    m.CaloriesKcal,
                    m.ProteinsG,
                    m.FatsG,
                    m.CarbsG,
                    m.IngredientsJson,
                    m.ProductsJson,
                    HasImage = EF.Functions.DataLength(m.ImageBytes) > 0
                })
                .ToListAsync(ct);

            // 2) десериализация уже в памяти
            var items = rows.Select(r => new
            {
                r.Id,
                r.CreatedAtUtc,
                r.DishName,
                r.WeightG,
                r.CaloriesKcal,
                r.ProteinsG,
                r.FatsG,
                r.CarbsG,
                Ingredients = string.IsNullOrWhiteSpace(r.IngredientsJson)
                    ? Array.Empty<string>()
                    : (System.Text.Json.JsonSerializer.Deserialize<string[]>(r.IngredientsJson!) ?? Array.Empty<string>()),
                Products = ProductJsonHelper.DeserializeProducts(r.ProductsJson),
                r.HasImage
            }).ToList();

            return Ok(new { total, offset, limit, items });
        }

        // ---------- Детали блюда ----------
        // GET /api/meals/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details([FromRoute] int id, CancellationToken ct)
        {
            var chatId = GetChatId();

            var m = await _db.Meals.AsNoTracking()
                .Where(x => x.ChatId == chatId && x.Id == id)
                .FirstOrDefaultAsync(ct);
            if (m == null) return NotFound();

            var ingredients = string.IsNullOrWhiteSpace(m.IngredientsJson)
                ? Array.Empty<string>()
                : (System.Text.Json.JsonSerializer.Deserialize<string[]>(m.IngredientsJson!) ?? Array.Empty<string>());

            Step1Snapshot? step1 = null;
            if (!string.IsNullOrWhiteSpace(m.Step1Json))
            {
                try
                {
                    step1 = System.Text.Json.JsonSerializer.Deserialize<Step1Snapshot>(
                        m.Step1Json!,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { /* ignore bad JSON */ }
            }

            var products = ProductJsonHelper.DeserializeProducts(m.ProductsJson);

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
                Ingredients = ingredients,
                Products = products,
                Step1 = step1,
                ReasoningPrompt = m.ReasoningPrompt,
                HasImage = m.ImageBytes != null && m.ImageBytes.Length > 0
            });
        }

        // ---------- Фото блюда ----------
        // GET /api/meals/{id}/image
        [HttpGet("{id:int}/image")]
        public async Task<IActionResult> Image([FromRoute] int id, CancellationToken ct)
        {
            var chatId = GetChatId();
            var m = await _db.Meals.AsNoTracking()
                .Where(x => x.ChatId == chatId && x.Id == id)
                .Select(x => new { x.ImageBytes, x.FileMime })
                .FirstOrDefaultAsync(ct);

            if (m == null || m.ImageBytes == null || m.ImageBytes.Length == 0) return NotFound();

            var mime = string.IsNullOrWhiteSpace(m.FileMime) ? "image/jpeg" : m.FileMime!;
            return File(m.ImageBytes, mime);
        }

        // ---------- Загрузка фото ----------
        // POST /api/meals/upload  (multipart/form-data: image)
        [HttpPost("upload")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> Upload([FromForm] IFormFile image, CancellationToken ct)
        {
            if (image == null || image.Length == 0) return BadRequest("image required");

            await using var ms = new MemoryStream();
            await image.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var chatId = GetChatId();
            var pending = new PendingMeal
            {
                ChatId = chatId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                FileMime = image.ContentType ?? "image/jpeg",
                ImageBytes = bytes,
                Attempts = 0
            };
            _db.PendingMeals.Add(pending);
            await _db.SaveChangesAsync(ct);

            return Ok(new { queued = true });
        }

        // ---------- Уточнение: текст ----------
        public sealed record ClarifyTextReq(string? note, DateTimeOffset? time);

        // POST /api/meals/{id}/clarify-text
        [HttpPost("{id:int}/clarify-text")]
        public async Task<IActionResult> ClarifyText([FromRoute] int id, [FromBody] ClarifyTextReq req, CancellationToken ct)
        {
            var chatId = GetChatId();
            var m = await _db.Meals.Where(x => x.ChatId == chatId && x.Id == id).FirstOrDefaultAsync(ct);
            if (m == null) return NotFound();

            if (string.IsNullOrWhiteSpace(req.note))
            {
                if (req.time.HasValue)
                {
                    m.CreatedAtUtc = req.time.Value.ToUniversalTime();
                    await _db.SaveChangesAsync(ct);
                }

                var ingredients = string.IsNullOrWhiteSpace(m.IngredientsJson)
                    ? Array.Empty<string>()
                    : (System.Text.Json.JsonSerializer.Deserialize<string[]>(m.IngredientsJson!) ?? Array.Empty<string>());

                Step1Snapshot? step1 = null;
                if (!string.IsNullOrWhiteSpace(m.Step1Json))
                {
                    try
                    {
                        step1 = System.Text.Json.JsonSerializer.Deserialize<Step1Snapshot>(
                            m.Step1Json!,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch { /* ignore */ }
                }

                var products = ProductJsonHelper.DeserializeProducts(m.ProductsJson);

                return Ok(new
                {
                    m.Id,
                    CreatedAtUtc = m.CreatedAtUtc,
                    Result = new
                    {
                        dish = m.DishName,
                        ingredients,
                        proteins_g = m.ProteinsG,
                        fats_g = m.FatsG,
                        carbs_g = m.CarbsG,
                        calories_kcal = m.CaloriesKcal,
                        weight_g = m.WeightG,
                        confidence = m.Confidence
                    },
                    Products = products,
                    Step1 = step1,
                    ReasoningPrompt = m.ReasoningPrompt,
                    CalcPlanJson = string.Empty
                });
            }

            var pending = new PendingClarify
            {
                ChatId = chatId,
                MealId = id,
                Note = req.note!,
                NewTime = req.time?.ToUniversalTime(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Attempts = 0
            };
            _db.PendingClarifies.Add(pending);
            await _db.SaveChangesAsync(ct);
            return Ok(new { queued = true });
        }

        // ---------- Уточнение: голос ----------
        // POST /api/meals/{id}/clarify-voice  (multipart/form-data: audio ; optional: language=ru)
        [HttpPost("{id:int}/clarify-voice")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> ClarifyVoice([FromRoute] int id, [FromForm] IFormFile audio, [FromForm] string? language, CancellationToken ct)
        {
            var chatId = GetChatId();
            var m = await _db.Meals.Where(x => x.ChatId == chatId && x.Id == id).FirstOrDefaultAsync(ct);
            if (m == null) return NotFound();
            if (audio == null || audio.Length == 0) return BadRequest("audio required");

            await using var ms = new MemoryStream();
            await audio.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var text = await _stt.TranscribeAsync(bytes, language ?? "ru", audio.FileName ?? "audio", audio.ContentType ?? "application/octet-stream", ct);
            if (string.IsNullOrWhiteSpace(text)) return BadRequest("stt_failed");

            // применяем как текстовое уточнение
            return await ClarifyText(id, new ClarifyTextReq(text!, null), ct);
        }

        // ---------- Excel отчёт ----------
        // GET /api/meals/report
        [HttpGet("report")]
        public async Task<IActionResult> Report(CancellationToken ct)
        {
            var chatId = GetChatId();
            var (stream, filename) = await _report.BuildUserReportAsync(chatId, ct);
            stream.Position = 0;
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        // ---------- Удаление блюда ----------
        // DELETE /api/meals/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
        {
            var chatId = GetChatId();
            var m = await _db.Meals.Where(x => x.ChatId == chatId && x.Id == id).FirstOrDefaultAsync(ct);
            if (m == null) return NotFound();

            _db.Meals.Remove(m);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
