using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodBot.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AppAuthController : ControllerBase
    {
        private readonly IAppAuthService _auth;

        public AppAuthController(IAppAuthService auth)
        {
            _auth = auth;
        }

        public sealed record RequestCodeResponse(string code, DateTimeOffset expiresAtUtc);

        // 1) Приложение просит КОД
        // POST /api/auth/request-code
        [HttpPost("request-code")]
        public async Task<ActionResult<RequestCodeResponse>> RequestCode(CancellationToken ct)
        {
            var resp = await _auth.RequestCodeAsync(ct);
            return Ok(resp);
        }

        public sealed record StatusResponse(bool linked, DateTimeOffset expiresAtUtc, int secondsLeft);

        // 2) Приложение проверяет статус линковки (нажата ли кнопка в боте)
        // GET /api/auth/status?code=XXXX
        [HttpGet("status")]
        public async Task<ActionResult<StatusResponse>> Status([FromQuery, Required] string code, CancellationToken ct)
        {
            var resp = await _auth.GetStatusAsync(code, ct);
            if (resp is null) return NotFound(new { error = "not_found" });
            return Ok(resp);
        }

        public sealed record ExchangeRequest([Required] string code, string? device);
        public sealed record ExchangeResponse(string accessToken, string tokenType, int expiresInSeconds, long chatId);

        // 3) Приложение обменивает КОД на JWT (если бот уже привязал ChatId)
        // POST /api/auth/exchange-startcode
        [HttpPost("exchange-startcode")]
        public async Task<ActionResult<ExchangeResponse>> ExchangeStartCode([FromBody] ExchangeRequest req, CancellationToken ct)
        {
            var res = await _auth.ExchangeStartCodeAsync(req.code, ct);
            if (res.Error != null) return BadRequest(new { error = res.Error });
            if (res.Pending) return Ok(new { status = "pending" });
            return Ok(res.Response);
        }
    }
}
