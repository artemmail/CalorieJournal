using FoodBot.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FoodBot.Services;

public sealed class AppAuthService : IAppAuthService
{
    private readonly BotDbContext _db;
    private readonly JwtService _jwt;
    private readonly IConfiguration _cfg;

    public AppAuthService(BotDbContext db, JwtService jwt, IConfiguration cfg)
    {
        _db = db;
        _jwt = jwt;
        _cfg = cfg;
    }

    private static string GenerateCode(int length = 8)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        var chars = bytes.Select(b => alphabet[b % alphabet.Length]);
        return new string(chars.ToArray());
    }

    public async Task<RequestCodeResponse> RequestCodeAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var ttlMin = int.TryParse(_cfg["Auth:StartCodeMinutes"], out var m) ? m : 15;

        string code;
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

        return new RequestCodeResponse(code, row.ExpiresAtUtc);
    }

    public async Task<StatusResponse?> GetStatusAsync(string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var row = await _db.StartCodes.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (row is null) return null;
        if (row.ExpiresAtUtc <= now) return new StatusResponse(false, row.ExpiresAtUtc, 0);
        var left = (int)Math.Max(0, (row.ExpiresAtUtc - now).TotalSeconds);
        var linked = row.ChatId.HasValue;
        return new StatusResponse(linked, row.ExpiresAtUtc, left);
    }

    public async Task<ExchangeStartCodeResult> ExchangeStartCodeAsync(string code, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var row = await _db.StartCodes.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (row is null) return new ExchangeStartCodeResult("not_found", false, null);
        if (row.ExpiresAtUtc <= now) return new ExchangeStartCodeResult("expired", false, null);
        if (row.ConsumedAtUtc is not null) return new ExchangeStartCodeResult("already_used", false, null);
        if (row.ChatId is null) return new ExchangeStartCodeResult(null, true, null);

        var jwt = _jwt.Issue(row.ChatId.Value);
        var hours = int.TryParse(_cfg["Auth:AccessTokenHours"], out var h) ? h : 72;
        row.ConsumedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        var resp = new ExchangeResponse(jwt, "Bearer", hours * 3600, row.ChatId.Value);
        return new ExchangeStartCodeResult(null, false, resp);
    }
}
