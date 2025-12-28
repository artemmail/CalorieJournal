namespace FoodBot.Services;

public interface IAppAuthService
{
    Task<RequestCodeResponse> RequestCodeAsync(CancellationToken ct);
    Task<StatusResponse?> GetStatusAsync(string code, CancellationToken ct);
    Task<ExchangeStartCodeResult> ExchangeStartCodeAsync(string code, CancellationToken ct);
}

public sealed record RequestCodeResponse(string Code, DateTimeOffset ExpiresAtUtc);
public sealed record StatusResponse(bool Linked, DateTimeOffset ExpiresAtUtc, int SecondsLeft);
public sealed record ExchangeResponse(string AccessToken, string TokenType, int ExpiresInSeconds, long UserId);
public sealed record ExchangeStartCodeResult(string? Error, bool Pending, ExchangeResponse? Response);
