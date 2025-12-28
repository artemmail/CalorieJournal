using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FoodBot.Services;

/// <summary>
/// Выдаёт JWT. Ключ берётся из Auth:JwtKey. Поддержка форматов:
/// - обычная строка → UTF8; если &lt; 32 байт, используется SHA256(строка)
/// - "base64:&lt;...&gt;" → Base64
/// - просто Base64-строка (попробуем декодировать)
/// </summary>
public sealed class JwtService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _ttl;
    private readonly SigningCredentials _creds;

    public JwtService(IConfiguration cfg)
    {
        _issuer = cfg["Auth:Issuer"] ?? "foodbot";
        _audience = cfg["Auth:Audience"] ?? "foodbot.app";
        var hours = int.TryParse(cfg["Auth:AccessTokenHours"], out var h) ? h : 72;
        _ttl = TimeSpan.FromHours(hours);

        var keyRaw = cfg["Auth:JwtKey"] ?? throw new InvalidOperationException("Auth:JwtKey missing");
        var keyBytes = JwtKeyHelper.GetKeyBytes(keyRaw);
        _creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
    }

    public string Issue(long userId)
    {
        var now = DateTimeOffset.UtcNow;

        var claims = new[]
        {
            new Claim("user_id", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var jwt = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: (now + _ttl).UtcDateTime,
            signingCredentials: _creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

/// <summary>Единая нормализация ключа для HS256.</summary>
public static class JwtKeyHelper
{
    public static byte[] GetKeyBytes(string keyRaw)
    {
        if (string.IsNullOrWhiteSpace(keyRaw))
            throw new InvalidOperationException("Auth:JwtKey is empty");

        // формат "base64:XXXXX"
        const string prefix = "base64:";
        if (keyRaw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Require32(Convert.FromBase64String(keyRaw[prefix.Length..]));
        }

        // пробуем как Base64 без префикса
        try
        {
            var b64 = Convert.FromBase64String(keyRaw);
            if (b64.Length >= 32) return b64;
            // если декодировалось, но мало — падать не будем, пойдём дальше
        }
        catch { /* not base64 */ }

        // как обычная строка
        var bytes = Encoding.UTF8.GetBytes(keyRaw);

        // недостаточно — берём SHA256(строка), это всегда 32 байта
        if (bytes.Length < 32)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        return bytes;
    }

    private static byte[] Require32(byte[] key)
    {
        if (key.Length < 32)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(key); // 32 байта
        }
        return key;
    }
}
