using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public enum ExternalProvider
{
    Telegram = 0,
    Vk = 1
}

public class ExternalAccount
{
    [Key]
    public long Id { get; set; }

    public ExternalProvider Provider { get; set; }

    [MaxLength(128)]
    public string ExternalId { get; set; } = default!;

    [MaxLength(256)]
    public string? Username { get; set; }

    public long AppUserId { get; set; }

    public AppUser AppUser { get; set; } = default!;

    public DateTimeOffset LinkedAtUtc { get; set; }
}
