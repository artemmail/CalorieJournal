using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public sealed class AppRefreshToken
{
    [Key]
    public long Id { get; set; }

    public long AppUserId { get; set; }
    public long AppUserDeviceId { get; set; }

    [MaxLength(128)]
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    [MaxLength(128)]
    public string? ReplacedByTokenHash { get; set; }

    public AppUser AppUser { get; set; } = default!;
    public AppUserDevice AppUserDevice { get; set; } = default!;
}
