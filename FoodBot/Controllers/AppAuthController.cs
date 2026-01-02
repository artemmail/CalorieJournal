using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FoodBot.Data;
using FoodBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FoodBot.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AppAuthController : ControllerBase
    {
        private readonly IAppAuthService _auth;
        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _http;

        public AppAuthController(IAppAuthService auth, IConfiguration cfg, IHttpClientFactory http)
        {
            _auth = auth;
            _cfg = cfg;
            _http = http;
        }

        // 1) Приложение просит КОД
        // POST /api/auth/request-code
        [HttpPost("request-code")]
        public async Task<ActionResult<RequestCodeResponse>> RequestCode(CancellationToken ct)
        {
            var resp = await _auth.RequestCodeAsync(ct);
            return Ok(resp);
        }

        // 2) Приложение проверяет статус линковки (нажата ли кнопка в боте)
        // GET /api/auth/status?code=XXXX
        [HttpGet("status")]
        public async Task<ActionResult<StatusResponse>> Status([FromQuery, Required] string code, CancellationToken ct)
        {
            var resp = await _auth.GetStatusAsync(code, ct);
            if (resp is null) return NotFound(new { error = "not_found" });
            return Ok(resp);
        }

        public sealed record ExchangeRequest([Required] string Code, string? Device);
        public sealed record ExternalLoginRequest([Required] ExternalProvider Provider, [Required] string ExternalId, string? Username, string? Device);

        // 3) Приложение обменивает КОД на JWT (если бот уже привязал аккаунт)
        // POST /api/auth/exchange-startcode
        [HttpPost("exchange-startcode")]
        public async Task<ActionResult<ExchangeResponse>> ExchangeStartCode([FromBody] ExchangeRequest req, CancellationToken ct)
        {
            var res = await _auth.ExchangeStartCodeAsync(req.Code, ct);
            if (res.Error != null) return BadRequest(new { error = res.Error });
            if (res.Pending) return Ok(new { status = "pending" });
            return Ok(res.Response);
        }

        // 4) Вход через внешний провайдер (без Telegram)
        // POST /api/auth/external-login
        [HttpPost("external-login")]
        public async Task<ActionResult<ExchangeResponse>> ExternalLogin([FromBody] ExternalLoginRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.ExternalId))
                return BadRequest(new { error = "external_id_required" });

            var resp = await _auth.ExchangeExternalAsync(req.Provider, req.ExternalId, req.Username, ct);
            return Ok(resp);
        }

        // === VK OAuth (redirect flow) ===
        [HttpGet("vk/start")]
        public IActionResult StartVk([FromQuery] string? returnUrl)
        {
            var cfg = GetVkConfig();
            if (!cfg.IsValid)
                return BadRequest(new { error = "vk_config_missing" });

            var state = BuildState(returnUrl ?? cfg.PostLoginRedirect);
            var url = $"https://oauth.vk.com/authorize?client_id={Uri.EscapeDataString(cfg.ClientId!)}&display=page&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri!)}&scope={Uri.EscapeDataString(cfg.Scope)}&response_type=code&v=5.199&state={Uri.EscapeDataString(state)}";
            return Redirect(url);
        }

        [HttpGet("vk/callback")]
        public async Task<IActionResult> VkCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery(Name = "error_description")] string? errorDescription, CancellationToken ct)
        {
            var cfg = GetVkConfig();
            var returnUrl = cfg.PostLoginRedirect;
            if (!string.IsNullOrWhiteSpace(state) && TryParseState(state, out var parsedReturn))
                returnUrl = parsedReturn;

            if (!cfg.IsValid)
                return BuildPlainError("VK configuration is missing", returnUrl);

            if (!string.IsNullOrWhiteSpace(error))
                return BuildPlainError($"VK error: {error} {errorDescription}", returnUrl);

            if (string.IsNullOrWhiteSpace(code))
                return BuildPlainError("VK code is missing", returnUrl);

            var tokenUrl = $"https://oauth.vk.com/access_token?client_id={Uri.EscapeDataString(cfg.ClientId!)}&client_secret={Uri.EscapeDataString(cfg.ClientSecret!)}&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri!)}&code={Uri.EscapeDataString(code)}";

            VkTokenResponse? tokenResp;
            try
            {
                using var client = _http.CreateClient();
                using var resp = await client.GetAsync(tokenUrl, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                tokenResp = JsonSerializer.Deserialize<VkTokenResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (tokenResp is null || tokenResp.UserId is null)
                    return BuildPlainError("Failed to decode VK token response", returnUrl);
            }
            catch (Exception ex)
            {
                return BuildPlainError($"VK exchange failed: {ex.Message}", returnUrl);
            }

            ExchangeResponse jwt;
            try
            {
                jwt = await _auth.ExchangeExternalAsync(ExternalProvider.Vk, tokenResp.UserId.Value.ToString(), tokenResp.Email, ct);
            }
            catch (Exception ex)
            {
                return BuildPlainError($"Failed to issue token: {ex.Message}", returnUrl);
            }

            return BuildRedirectHtml(jwt, returnUrl);
        }

        private VkConfig GetVkConfig()
        {
            var section = _cfg.GetSection("Auth:Vk");
            return new VkConfig
            {
                ClientId = section["ClientId"],
                ClientSecret = section["ClientSecret"],
                RedirectUri = section["RedirectUri"],
                PostLoginRedirect = section["PostLoginRedirect"] ?? "/auth",
                Scope = section["Scope"] ?? "offline"
            };
        }

        private string BuildState(string? returnUrl)
        {
            var payload = $"{Guid.NewGuid():N}|{returnUrl ?? string.Empty}";
            var key = _cfg["Auth:Vk:StateKey"] ?? _cfg["Auth:JwtKey"] ?? "vk_state_key";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var combined = Encoding.UTF8.GetBytes(payload + "|" + Convert.ToHexString(sig));
            return ToBase64Url(combined);
        }

        private bool TryParseState(string state, out string? returnUrl)
        {
            returnUrl = null;
            try
            {
                var bytes = FromBase64Url(state);
                var text = Encoding.UTF8.GetString(bytes);
                var parts = text.Split('|', 3);
                if (parts.Length == 3)
                {
                    var payload = string.Join('|', parts[0], parts[1]);
                    var sigHex = parts[2];
                    var key = _cfg["Auth:Vk:StateKey"] ?? _cfg["Auth:JwtKey"] ?? "vk_state_key";
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
                    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
                    if (!string.Equals(expected, sigHex, StringComparison.OrdinalIgnoreCase))
                        return false;
                    returnUrl = parts[1];
                    return true;
                }
            }
            catch { }
            return false;
        }

        private IActionResult BuildPlainError(string message, string? returnUrl)
        {
            var safeMsg = message ?? "vk_error";
            var html = $"<!doctype html><html><body><p>{System.Net.WebUtility.HtmlEncode(safeMsg)}</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(returnUrl ?? "/auth")}\">Назад</a></p></body></html>";
            return Content(html, "text/html", Encoding.UTF8);
        }

        private IActionResult BuildRedirectHtml(ExchangeResponse token, string? returnUrl)
        {
            var target = string.IsNullOrWhiteSpace(returnUrl) ? "/auth" : returnUrl!;
            var expMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + token.ExpiresInSeconds * 1000L;
            string JsEncode(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");

            var html = $@"<!doctype html><html><body><script>(function(){{
  var token='{JsEncode(token.AccessToken)}';
  var expMs={expMs};
  localStorage.setItem('foodbot.jwt', token);
  localStorage.setItem('foodbot.jwt.exp', String(expMs));
  if (window.opener && window.opener !== window){{
    try {{ window.opener.postMessage({{ type: 'foodbot-auth', provider: 'vk', token: token, expMs: expMs }}, '*'); }} catch(e){{}}
    window.close();
  }} else {{
    window.location.replace('{JsEncode(target)}#vk=ok');
  }}
}})();</script><p>VK login завершён, перенаправляем...</p></body></html>";
            return Content(html, "text/html", Encoding.UTF8);
        }

        private static string ToBase64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] FromBase64Url(string s)
        {
            var padded = s.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        private sealed record VkTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; init; }
            [JsonPropertyName("expires_in")]
            public int? ExpiresIn { get; init; }
            [JsonPropertyName("user_id")]
            public long? UserId { get; init; }
            [JsonPropertyName("email")]
            public string? Email { get; init; }
        }

        private sealed record VkConfig
        {
            public string? ClientId { get; init; }
            public string? ClientSecret { get; init; }
            public string? RedirectUri { get; init; }
            public string? PostLoginRedirect { get; init; }
            public string Scope { get; init; } = "offline";
            public bool IsValid => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RedirectUri);
        }
    }
}
