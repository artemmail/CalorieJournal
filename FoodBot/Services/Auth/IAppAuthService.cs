namespace FoodBot.Services;

public interface IAppAuthService
{
    Task<AuthSessionResponse> StartAnonymousAsync(string? installId, string? device, CancellationToken ct);
    Task<AuthSessionResponse?> RefreshAsync(string refreshToken, string? installId, string? device, CancellationToken ct);

    Task<RequestCodeResponse> RequestCodeAsync(long? appUserId, CancellationToken ct);
    Task<StatusResponse?> GetStatusAsync(string code, CancellationToken ct);
    Task<ExchangeStartCodeResult> ExchangeStartCodeAsync(string code, string? installId, string? device, CancellationToken ct);

    Task<IReadOnlyList<AuthProviderInfo>> GetProvidersAsync(long appUserId, CancellationToken ct);
    Task<TelegramLinkCodeResult> LinkTelegramStartCodeAsync(long telegramChatId, string? telegramUsername, string code, CancellationToken ct);
}

public sealed record RequestCodeResponse(string Code, DateTimeOffset ExpiresAtUtc);
public sealed record StatusResponse(bool Linked, DateTimeOffset ExpiresAtUtc, int SecondsLeft);

public sealed record AuthSessionResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    int RefreshExpiresInSeconds,
    long AppUserId,
    long ChatId,
    bool IsAnonymous);

public sealed record ExchangeResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    int RefreshExpiresInSeconds,
    long AppUserId,
    long ChatId,
    bool IsAnonymous);

public sealed record ExchangeStartCodeResult(string? Error, bool Pending, ExchangeResponse? Response);

public sealed record AuthProviderInfo(
    string Provider,
    string ExternalUserId,
    string? ExternalUsername,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? VerifiedAtUtc);

public sealed record TelegramLinkCodeResult(string Status, string Message, bool Success);
