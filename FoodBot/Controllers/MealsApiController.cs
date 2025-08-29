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

            var conv = await _nutrition.AnalyzeAsync(bytes, ct: ct);
            if (conv is null) return BadRequest("analyze_failed");

            var chatId = GetChatId();
            var entry = new MealEntry
            {
                ChatId = chatId,
                UserId = 0,
                Username = "app",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                FileId = null,
                FileMime = image.ContentType ?? "image/jpeg",
                ImageBytes = bytes,
                DishName = conv.Result.dish,
                IngredientsJson = System.Text.Json.JsonSerializer.Serialize(conv.Result.ingredients),
                ProductsJson = ProductJsonHelper.BuildProductsJson(conv.CalcPlanJson),
                ProteinsG = conv.Result.proteins_g,
                FatsG = conv.Result.fats_g,
                CarbsG = conv.Result.carbs_g,
                CaloriesKcal = conv.Result.calories_kcal,
                Confidence = conv.Result.confidence,
                WeightG = conv.Result.weight_g,
                Model = "app",
                Step1Json = System.Text.Json.JsonSerializer.Serialize(conv.Step1),
                ReasoningPrompt = conv.ReasoningPrompt
            };
            _db.Meals.Add(entry);
            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                entry.Id,
                Result = conv.Result,
                Products = ProductJsonHelper.DeserializeProducts(entry.ProductsJson),
                Step1 = conv.Step1,
                conv.ReasoningPrompt,
                conv.CalcPlanJson
            });
        }

        // ---------- Уточнение: текст ----------
        public sealed record ClarifyTextReq(string note);

        // POST /api/meals/{id}/clarify-text
        [HttpPost("{id:int}/clarify-text")]
        public async Task<IActionResult> ClarifyText([FromRoute] int id, [FromBody] ClarifyTextReq req, CancellationToken ct)
        {
            var chatId = GetChatId();
            var m = await _db.Meals.Where(x => x.ChatId == chatId && x.Id == id).FirstOrDefaultAsync(ct);
            if (m == null) return NotFound();

            NutritionConversation? conv2 = null;

            if (!string.IsNullOrWhiteSpace(m.Step1Json))
            {
                try
                {
                    var step1 = System.Text.Json.JsonSerializer.Deserialize<Step1Snapshot>(
                        m.Step1Json!,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (step1 is not null)
                        conv2 = await _nutrition.ClarifyFromStep1Async(step1, req.note ?? "", ct);
                }
                catch { conv2 = null; }
            }

            if (conv2 is null && m.ImageBytes is { Length: > 0 })
            {
                conv2 = await _nutrition.AnalyzeWithNoteAsync(m.ImageBytes, req.note ?? "", ct: ct);
            }

            if (conv2 is null) return BadRequest("clarify_failed");

            m.DishName = conv2.Result.dish;
            m.IngredientsJson = System.Text.Json.JsonSerializer.Serialize(conv2.Result.ingredients);
            m.ProductsJson = ProductJsonHelper.BuildProductsJson(conv2.CalcPlanJson);
            m.ProteinsG = conv2.Result.proteins_g;
            m.FatsG = conv2.Result.fats_g;
            m.CarbsG = conv2.Result.carbs_g;
            m.CaloriesKcal = conv2.Result.calories_kcal;
            m.Confidence = conv2.Result.confidence;
            m.WeightG = conv2.Result.weight_g;
            m.ReasoningPrompt = conv2.ReasoningPrompt;
            m.CreatedAtUtc = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                m.Id,
                Result = conv2.Result,
                Products = ProductJsonHelper.DeserializeProducts(m.ProductsJson),
                Step1 = conv2.Step1,
                conv2.ReasoningPrompt,
                conv2.CalcPlanJson
            });
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
            return await ClarifyText(id, new ClarifyTextReq(text!), ct);
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
