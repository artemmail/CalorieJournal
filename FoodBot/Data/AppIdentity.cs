using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public sealed class AppIdentity
{
    [Key]
    public long Id { get; set; }

    public long AppUserId { get; set; }

    [MaxLength(32)]
    public string Provider { get; set; } = default!;

    [MaxLength(128)]
    public string ExternalUserId { get; set; } = default!;

    [MaxLength(256)]
    public string? ExternalUsername { get; set; }

    [MaxLength(256)]
    public string? Email { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }

    public AppUser AppUser { get; set; } = default!;
}
