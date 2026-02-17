using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

public class AppStartCode
{
    public int Id { get; set; }

    // Short one-time code shown in app and sent to Telegram bot.
    public string Code { get; set; } = default!;

    // Telegram chat id captured when user confirms code in bot.
    public long? ChatId { get; set; }

    public long? AppUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }

    [ForeignKey(nameof(AppUserId))]
    public AppUser? AppUser { get; set; }
}
