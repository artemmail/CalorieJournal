using System.ComponentModel.DataAnnotations;
using FoodBot.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodBot.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AppAuthController : ControllerBase
{
    private readonly IAppAuthService _auth;

    public AppAuthController(IAppAuthService auth)
    {
        _auth = auth;
    }

    public sealed record AnonymousStartRequest(string? InstallId, string? Device);
    public sealed record RefreshRequest([Required] string RefreshToken, string? InstallId, string? Device);
    public sealed record ExchangeRequest([Required] string Code, string? Device, string? InstallId);

    // New flow: app starts as anonymous user without mandatory signup.
    [AllowAnonymous]
    [HttpPost("anonymous/start")]
    public async Task<ActionResult<AuthSessionResponse>> AnonymousStart([FromBody] AnonymousStartRequest? req, CancellationToken ct)
    {
        var res = await _auth.StartAnonymousAsync(req?.InstallId, req?.Device, ct);
        return Ok(res);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var res = await _auth.RefreshAsync(req.RefreshToken, req.InstallId, req.Device, ct);
        if (res is null) return Unauthorized(new { error = "invalid_refresh" });
        return Ok(res);
    }

    // Legacy-compatible endpoint; when authorized it creates link code for current app_user_id.
    [AllowAnonymous]
    [HttpPost("request-code")]
    public async Task<ActionResult<RequestCodeResponse>> RequestCode(CancellationToken ct)
    {
        long? appUserId = null;
        if (User?.Identity?.IsAuthenticated == true)
            appUserId = User.GetAppUserId();

        var resp = await _auth.RequestCodeAsync(appUserId, ct);
        return Ok(resp);
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("link/telegram/request-code")]
    public async Task<ActionResult<RequestCodeResponse>> RequestTelegramCode(CancellationToken ct)
    {
        var appUserId = User.GetAppUserId();
        var resp = await _auth.RequestCodeAsync(appUserId, ct);
        return Ok(resp);
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public async Task<ActionResult<StatusResponse>> Status([FromQuery, Required] string code, CancellationToken ct)
    {
        var resp = await _auth.GetStatusAsync(code, ct);
        if (resp is null) return NotFound(new { error = "not_found" });
        return Ok(resp);
    }

    [AllowAnonymous]
    [HttpPost("exchange-startcode")]
    public async Task<ActionResult<ExchangeResponse>> ExchangeStartCode([FromBody] ExchangeRequest req, CancellationToken ct)
    {
        var res = await _auth.ExchangeStartCodeAsync(req.Code, req.InstallId, req.Device, ct);
        if (res.Error != null) return BadRequest(new { error = res.Error });
        if (res.Pending) return Ok(new { status = "pending" });
        return Ok(res.Response);
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<AuthProviderInfo>>> Providers(CancellationToken ct)
    {
        var appUserId = User.GetAppUserId();
        var providers = await _auth.GetProvidersAsync(appUserId, ct);
        return Ok(providers);
    }
}
