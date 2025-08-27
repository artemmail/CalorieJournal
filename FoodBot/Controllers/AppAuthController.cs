using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodBot.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AppAuthController : ControllerBase
    {
        private readonly BotDbContext _db;
        private readonly JwtService _jwt;
        private readonly IConfiguration _cfg;

        public AppAuthController(BotDbContext db, JwtService jwt, IConfiguration cfg)
        {
            _db = db;
            _jwt = jwt;
            _cfg = cfg;
        }

        private static string GenerateCode(int length = 8)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // без похожих символов
            var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            var chars = bytes.Select(b => alphabet[b % alphabet.Length]);
            return new string(chars.ToArray());
        }

        public sealed record RequestCodeResponse(string code, DateTimeOffset expiresAtUtc);

        // 1) Приложение просит КОД
        // POST /api/auth/request-code
        [HttpPost("request-code")]
        public async Task<ActionResult<RequestCodeResponse>> RequestCode(CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var ttlMin = int.TryParse(_cfg["Auth:StartCodeMinutes"], out var m) ? m : 15;

            string code;
            // гарантируем уникальность кода
            do { code = GenerateCode(8); }
            while (await _db.StartCodes.AnyAsync(x => x.Code == code && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now, ct));

            var row = new AppStartCode
            {
                Code = code,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(ttlMin)
            };
            _db.StartCodes.Add(row);
            await _db.SaveChangesAsync(ct);

            return Ok(new RequestCodeResponse(code, row.ExpiresAtUtc));
        }

        public sealed record StatusResponse(bool linked, DateTimeOffset expiresAtUtc, int secondsLeft);

        // 2) Приложение проверяет статус линковки (нажата ли кнопка в боте)
        // GET /api/auth/status?code=XXXX
        [HttpGet("status")]
        public async Task<ActionResult<StatusResponse>> Status([FromQuery, Required] string code, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var row = await _db.StartCodes.FirstOrDefaultAsync(x => x.Code == code, ct);
            if (row is null) return NotFound(new { error = "not_found" });

            if (row.ExpiresAtUtc <= now) return Ok(new StatusResponse(false, row.ExpiresAtUtc, 0));
            var left = (int)Math.Max(0, (row.ExpiresAtUtc - now).TotalSeconds);

            var linked = row.ChatId.HasValue;
            return Ok(new StatusResponse(linked, row.ExpiresAtUtc, left));
        }

        public sealed record ExchangeRequest([Required] string code, string? device);
        public sealed record ExchangeResponse(string accessToken, string tokenType, int expiresInSeconds, long chatId);

        // 3) Приложение обменивает КОД на JWT (если бот уже привязал ChatId)
        // POST /api/auth/exchange-startcode
        [HttpPost("exchange-startcode")]
        public async Task<ActionResult<ExchangeResponse>> ExchangeStartCode([FromBody] ExchangeRequest req, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var row = await _db.StartCodes.FirstOrDefaultAsync(x => x.Code == req.code, ct);
            if (row is null) return BadRequest(new { error = "not_found" });
            if (row.ExpiresAtUtc <= now) return BadRequest(new { error = "expired" });
            if (row.ConsumedAtUtc is not null) return BadRequest(new { error = "already_used" });

            if (row.ChatId is null)
            {
                // ещё не ввели код в боте
                return Ok(new { status = "pending" });
            }

            // выдаём JWT и помечаем как использованный
            var jwt = _jwt.Issue(row.ChatId.Value);
            var hours = int.TryParse(_cfg["Auth:AccessTokenHours"], out var h) ? h : 72;
            row.ConsumedAtUtc = now;
            await _db.SaveChangesAsync(ct);

            return Ok(new ExchangeResponse(jwt, "Bearer", hours * 3600, row.ChatId.Value));
        }
    }
}
