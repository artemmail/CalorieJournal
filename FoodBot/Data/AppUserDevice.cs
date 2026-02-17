using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public sealed class AppUserDevice
{
    [Key]
    public long Id { get; set; }

    public long AppUserId { get; set; }

    [MaxLength(128)]
    public string InstallId { get; set; } = default!;

    [MaxLength(256)]
    public string? DeviceName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public AppUser AppUser { get; set; } = default!;
}
