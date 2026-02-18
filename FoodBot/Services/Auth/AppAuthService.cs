using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FoodBot.Services;

public sealed class AppAuthService : IAppAuthService
{
    private const string ProviderTelegram = "telegram";

    private readonly BotDbContext _db;
    private readonly JwtService _jwt;
    private readonly IConfiguration _cfg;

    public AppAuthService(BotDbContext db, JwtService jwt, IConfiguration cfg)
    {
        _db = db;
        _jwt = jwt;
        _cfg = cfg;
    }

    private int StartCodeMinutes => int.TryParse(_cfg["Auth:StartCodeMinutes"], out var m) ? Math.Clamp(m, 5, 120) : 15;
    private int RefreshTokenDays => int.TryParse(_cfg["Auth:RefreshTokenDays"], out var d) ? Math.Clamp(d, 1, 180) : 30;

    private static string GenerateCode(int length = 8)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        var chars = bytes.Select(b => alphabet[b % alphabet.Length]);
        return new string(chars.ToArray());
    }

    private static string GenerateOpaqueToken(int bytes = 32)
    {
        var data = new byte[bytes];
        RandomNumberGenerator.Fill(data);
        return Convert.ToHexString(data).ToLowerInvariant();
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<long> GenerateUniqueStorageChatIdAsync(CancellationToken ct)
    {
        while (true)
        {
            var millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x1FFFFFFFFFFL; // 41 bits
            var noise = RandomNumberGenerator.GetInt32(0, 1 << 20); // 20 bits
            var candidate = long.MinValue + ((millis << 20) | (uint)noise);
            var exists = await _db.AppUsers.AnyAsync(x => x.StorageChatId == candidate, ct);
            if (!exists)
                return candidate;
        }
    }

    private async Task<AppUser> CreateAnonymousUserAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var storageChatId = await GenerateUniqueStorageChatIdAsync(ct);

        var user = new AppUser
        {
            StorageChatId = storageChatId,
            IsAnonymous = true,
            Status = "active",
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<AppUser> GetOrCreateTelegramUserAsync(long telegramChatId, CancellationToken ct)
    {
        var byIdentity = await _db.AppIdentities
            .AsNoTracking()
            .Where(x => x.Provider == ProviderTelegram && x.ExternalUserId == telegramChatId.ToString())
            .Select(x => x.AppUserId)
            .FirstOrDefaultAsync(ct);

        if (byIdentity != 0)
        {
            var resolved = await _db.AppUsers.FirstAsync(x => x.Id == byIdentity, ct);
            resolved.LastSeenAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return resolved;
        }

        var byStorage = await _db.AppUsers.FirstOrDefaultAsync(x => x.StorageChatId == telegramChatId, ct);
        if (byStorage is not null)
        {
            byStorage.IsAnonymous = false;
            byStorage.LastSeenAtUtc = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return byStorage;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new AppUser
        {
            StorageChatId = telegramChatId,
            IsAnonymous = false,
            Status = "active",
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<AppUser> EnsureAppUserAsync(long appUserId, CancellationToken ct)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == appUserId, ct);
        if (user is not null)
            return user;

        // Backward compatibility: old tokens can contain only chat_id.
        user = await _db.AppUsers.FirstOrDefaultAsync(x => x.StorageChatId == appUserId, ct);
        if (user is not null)
            return user;

        var now = DateTimeOffset.UtcNow;
        user = new AppUser
        {
            StorageChatId = appUserId,
            IsAnonymous = false,
            Status = "active",
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<AppUserDevice> GetOrCreateDeviceAsync(long appUserId, string installId, string? deviceName, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var device = await _db.AppUserDevices.FirstOrDefaultAsync(x => x.InstallId == installId, ct);

        if (device is null)
        {
            device = new AppUserDevice
            {
                AppUserId = appUserId,
                InstallId = installId,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
                CreatedAtUtc = now,
                LastSeenAtUtc = now
            };
            _db.AppUserDevices.Add(device);
        }
        else
        {
            device.AppUserId = appUserId;
            device.DeviceName = string.IsNullOrWhiteSpace(deviceName) ? device.DeviceName : deviceName.Trim();
            device.LastSeenAtUtc = now;
            device.RevokedAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        return device;
    }

    private async Task<AuthSessionResponse> IssueSessionAsync(AppUser user, string? installId, string? device, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(installId))
            installId = GenerateOpaqueToken(16);

        var now = DateTimeOffset.UtcNow;
        user.LastSeenAtUtc = now;

        var appDevice = await GetOrCreateDeviceAsync(user.Id, installId, device, ct);

        var refreshToken = GenerateOpaqueToken(32);
        var refreshHash = HashToken(refreshToken);
        var refreshExpires = now.AddDays(RefreshTokenDays);

        var refreshRow = new AppRefreshToken
        {
            AppUserId = user.Id,
            AppUserDeviceId = appDevice.Id,
            TokenHash = refreshHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires
        };

        _db.AppRefreshTokens.Add(refreshRow);
        await _db.SaveChangesAsync(ct);

        var access = _jwt.Issue(user.Id, user.StorageChatId);
        return new AuthSessionResponse(
            AccessToken: access,
            RefreshToken: refreshToken,
            TokenType: "Bearer",
            ExpiresInSeconds: _jwt.AccessTokenExpiresInSeconds,
            RefreshExpiresInSeconds: (int)Math.Max(60, (refreshExpires - now).TotalSeconds),
            AppUserId: user.Id,
            ChatId: user.StorageChatId,
            IsAnonymous: user.IsAnonymous);
    }

    private async Task<bool> EnsureTelegramIdentityAsync(long appUserId, long telegramChatId, string? telegramUsername, CancellationToken ct)
    {
        var extId = telegramChatId.ToString();
        var now = DateTimeOffset.UtcNow;

        var existing = await _db.AppIdentities
            .FirstOrDefaultAsync(x => x.Provider == ProviderTelegram && x.ExternalUserId == extId, ct);

        if (existing is null)
        {
            _db.AppIdentities.Add(new AppIdentity
            {
                AppUserId = appUserId,
                Provider = ProviderTelegram,
                ExternalUserId = extId,
                ExternalUsername = string.IsNullOrWhiteSpace(telegramUsername) ? null : telegramUsername.Trim(),
                CreatedAtUtc = now,
                VerifiedAtUtc = now
            });
            await _db.SaveChangesAsync(ct);
            return true;
        }

        if (existing.AppUserId != appUserId)
            return false;

        existing.ExternalUsername = string.IsNullOrWhiteSpace(telegramUsername) ? existing.ExternalUsername : telegramUsername.Trim();
        existing.VerifiedAtUtc ??= now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task MoveDomainDataAsync(long sourceChatId, long targetChatId, CancellationToken ct)
    {
        if (sourceChatId == targetChatId)
            return;

        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Meals SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PendingMeals SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PendingClarifies SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AnalysisReports2 SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PeriodPdfJobs SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE AnalysisPdfJobs SET ChatId = {targetChatId} WHERE ChatId = {sourceChatId}", ct);

        var sourceCard = await _db.PersonalCards.FirstOrDefaultAsync(x => x.ChatId == sourceChatId, ct);
        var targetCard = await _db.PersonalCards.FirstOrDefaultAsync(x => x.ChatId == targetChatId, ct);
        if (sourceCard is not null && targetCard is null)
        {
            sourceCard.ChatId = targetChatId;
        }
        else if (sourceCard is not null && targetCard is not null)
        {
            _db.PersonalCards.Remove(sourceCard);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthSessionResponse> StartAnonymousAsync(string? installId, string? device, CancellationToken ct)
    {
        var user = await CreateAnonymousUserAsync(ct);
        return await IssueSessionAsync(user, installId, device, ct);
    }

    public async Task<AuthSessionResponse?> RefreshAsync(string refreshToken, string? installId, string? device, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var now = DateTimeOffset.UtcNow;
        var hash = HashToken(refreshToken);

        var row = await _db.AppRefreshTokens
            .Include(x => x.AppUser)
            .Include(x => x.AppUserDevice)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (row is null || row.RevokedAtUtc is not null || row.ExpiresAtUtc <= now)
            return null;

        if (!string.IsNullOrWhiteSpace(installId) && !string.Equals(row.AppUserDevice.InstallId, installId, StringComparison.Ordinal))
            return null;

        row.RevokedAtUtc = now;

        var newRefreshToken = GenerateOpaqueToken(32);
        var newRefreshHash = HashToken(newRefreshToken);
        row.ReplacedByTokenHash = newRefreshHash;

        row.AppUser.LastSeenAtUtc = now;

        var replacement = new AppRefreshToken
        {
            AppUserId = row.AppUserId,
            AppUserDeviceId = row.AppUserDeviceId,
            TokenHash = newRefreshHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(RefreshTokenDays)
        };

        if (!string.IsNullOrWhiteSpace(device))
            row.AppUserDevice.DeviceName = device.Trim();
        row.AppUserDevice.LastSeenAtUtc = now;

        _db.AppRefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(ct);

        var access = _jwt.Issue(row.AppUserId, row.AppUser.StorageChatId);
        return new AuthSessionResponse(
            AccessToken: access,
            RefreshToken: newRefreshToken,
            TokenType: "Bearer",
            ExpiresInSeconds: _jwt.AccessTokenExpiresInSeconds,
            RefreshExpiresInSeconds: (int)Math.Max(60, (replacement.ExpiresAtUtc - now).TotalSeconds),
            AppUserId: row.AppUserId,
            ChatId: row.AppUser.StorageChatId,
            IsAnonymous: row.AppUser.IsAnonymous);
    }

    public async Task<RequestCodeResponse> RequestCodeAsync(long? appUserId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        AppUser user;
        if (appUserId.HasValue)
        {
            user = await EnsureAppUserAsync(appUserId.Value, ct);
        }
        else
        {
            user = await CreateAnonymousUserAsync(ct);
        }

        string code;
        do { code = GenerateCode(8); }
        while (await _db.StartCodes.AnyAsync(x => x.Code == code && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now, ct));

        var row = new AppStartCode
        {
            Code = code,
            AppUserId = user.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(StartCodeMinutes)
        };

        _db.StartCodes.Add(row);
        await _db.SaveChangesAsync(ct);

        return new RequestCodeResponse(code, row.ExpiresAtUtc);
    }

    public async Task<StatusResponse?> GetStatusAsync(string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var row = await _db.StartCodes.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (row is null) return null;

        if (row.ExpiresAtUtc <= now)
            return new StatusResponse(false, row.ExpiresAtUtc, 0);

        var left = (int)Math.Max(0, (row.ExpiresAtUtc - now).TotalSeconds);
        var linked = row.ChatId.HasValue;
        return new StatusResponse(linked, row.ExpiresAtUtc, left);
    }

    public async Task<ExchangeStartCodeResult> ExchangeStartCodeAsync(string code, string? installId, string? device, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var row = await _db.StartCodes
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Code == code, ct);

        if (row is null) return new ExchangeStartCodeResult("not_found", false, null);
        if (row.ExpiresAtUtc <= now) return new ExchangeStartCodeResult("expired", false, null);
        if (row.ConsumedAtUtc is not null) return new ExchangeStartCodeResult("already_used", false, null);
        if (row.ChatId is null) return new ExchangeStartCodeResult(null, true, null);

        AppUser user;
        if (row.AppUserId.HasValue)
        {
            user = row.AppUser ?? await _db.AppUsers.FirstAsync(x => x.Id == row.AppUserId.Value, ct);
        }
        else
        {
            user = await GetOrCreateTelegramUserAsync(row.ChatId.Value, ct);
            row.AppUserId = user.Id;
        }

        // If account was linked through Telegram, keep storage key equal to telegram chat for bot/app parity.
        if (user.StorageChatId != row.ChatId.Value)
        {
            var targetUser = await _db.AppUsers.FirstOrDefaultAsync(x => x.StorageChatId == row.ChatId.Value, ct);
            if (targetUser is not null && targetUser.Id != user.Id)
            {
                await MoveDomainDataAsync(user.StorageChatId, targetUser.StorageChatId, ct);

                var identities = await _db.AppIdentities.Where(x => x.AppUserId == user.Id).ToListAsync(ct);
                foreach (var identity in identities)
                {
                    var hasSame = await _db.AppIdentities.AnyAsync(
                        x => x.AppUserId == targetUser.Id && x.Provider == identity.Provider && x.ExternalUserId == identity.ExternalUserId,
                        ct);
                    if (hasSame)
                    {
                        _db.AppIdentities.Remove(identity);
                    }
                    else
                    {
                        identity.AppUserId = targetUser.Id;
                    }
                }

                user.Status = "merged";
                row.AppUserId = targetUser.Id;
                user = targetUser;
            }
            else
            {
                await MoveDomainDataAsync(user.StorageChatId, row.ChatId.Value, ct);
                user.StorageChatId = row.ChatId.Value;
            }
        }

        user.IsAnonymous = false;
        user.LastSeenAtUtc = now;

        if (!await EnsureTelegramIdentityAsync(user.Id, row.ChatId.Value, null, ct))
            return new ExchangeStartCodeResult("identity_conflict", false, null);

        row.ConsumedAtUtc = now;
        await _db.SaveChangesAsync(ct);

        var session = await IssueSessionAsync(user, installId, device, ct);
        var resp = new ExchangeResponse(
            AccessToken: session.AccessToken,
            RefreshToken: session.RefreshToken,
            TokenType: session.TokenType,
            ExpiresInSeconds: session.ExpiresInSeconds,
            RefreshExpiresInSeconds: session.RefreshExpiresInSeconds,
            AppUserId: session.AppUserId,
            ChatId: session.ChatId,
            IsAnonymous: session.IsAnonymous);

        return new ExchangeStartCodeResult(null, false, resp);
    }

    public async Task<IReadOnlyList<AuthProviderInfo>> GetProvidersAsync(long appUserId, CancellationToken ct)
    {
        var user = await EnsureAppUserAsync(appUserId, ct);

        var rows = await _db.AppIdentities
            .AsNoTracking()
            .Where(x => x.AppUserId == user.Id)
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new AuthProviderInfo(
                x.Provider,
                x.ExternalUserId,
                x.ExternalUsername,
                x.CreatedAtUtc,
                x.VerifiedAtUtc))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<TelegramLinkCodeResult> LinkTelegramStartCodeAsync(long telegramChatId, string? telegramUsername, string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var row = await _db.StartCodes
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Code == code, ct);

        if (row is null)
            return new TelegramLinkCodeResult("not_found", "Код не найден. Проверьте и попробуйте ещё раз.", false);

        if (row.ExpiresAtUtc <= now)
            return new TelegramLinkCodeResult("expired", "Код просрочен. Попросите новый в приложении.", false);

        if (row.ConsumedAtUtc is not null)
            return new TelegramLinkCodeResult("already_used", "Этот код уже использован. Попросите новый в приложении.", false);

        if (row.ChatId == telegramChatId)
            return new TelegramLinkCodeResult("already_linked", "Этот код уже привязан к вашему аккаунту. Можете вернуться в приложение и нажать «Обновить».", true);

        AppUser sourceUser;
        long sourceUserId;
        if (row.AppUserId.HasValue)
        {
            sourceUser = row.AppUser ?? await _db.AppUsers.FirstAsync(x => x.Id == row.AppUserId.Value, ct);
            sourceUserId = sourceUser.Id;
        }
        else
        {
            sourceUser = await CreateAnonymousUserAsync(ct);
            row.AppUserId = sourceUser.Id;
            sourceUserId = sourceUser.Id;
        }

        var targetUser = await GetOrCreateTelegramUserAsync(telegramChatId, ct);

        if (targetUser.Id != sourceUser.Id)
        {
            await MoveDomainDataAsync(sourceUser.StorageChatId, targetUser.StorageChatId, ct);

            var identities = await _db.AppIdentities.Where(x => x.AppUserId == sourceUser.Id).ToListAsync(ct);
            foreach (var identity in identities)
            {
                var hasSame = await _db.AppIdentities.AnyAsync(
                    x => x.AppUserId == targetUser.Id && x.Provider == identity.Provider && x.ExternalUserId == identity.ExternalUserId,
                    ct);
                if (hasSame)
                {
                    _db.AppIdentities.Remove(identity);
                }
                else
                {
                    identity.AppUserId = targetUser.Id;
                }
            }

            sourceUser.Status = "merged";
            row.AppUserId = targetUser.Id;
        }

        row.ChatId = telegramChatId;

        if (!await EnsureTelegramIdentityAsync(targetUser.Id, telegramChatId, telegramUsername, ct))
            return new TelegramLinkCodeResult("identity_conflict", "Этот Telegram уже привязан к другому аккаунту. Нужен merge в приложении.", false);

        // Mark all active codes for the same app account as linked so the app can
        // finish login even if it polls a recently issued sibling code.
        var activeCodes = await _db.StartCodes
            .Where(x =>
                x.ConsumedAtUtc == null &&
                x.ExpiresAtUtc > now &&
                x.ChatId == null &&
                (x.AppUserId == targetUser.Id || x.AppUserId == sourceUserId))
            .ToListAsync(ct);
        foreach (var startCode in activeCodes)
        {
            startCode.ChatId = telegramChatId;
            startCode.AppUserId = targetUser.Id;
        }

        targetUser.IsAnonymous = false;
        targetUser.LastSeenAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return new TelegramLinkCodeResult("linked", "✅ Код принят. Вернитесь в приложение и нажмите «Обновить».", true);
    }
}
