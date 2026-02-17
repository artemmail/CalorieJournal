using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public sealed class AppUser
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Legacy-compatible storage key used by existing domain tables that are keyed by ChatId.
    /// </summary>
    public long StorageChatId { get; set; }

    public bool IsAnonymous { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "active";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
