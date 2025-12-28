using System;

namespace FoodBot.Data
{
    public class AppMagicToken
    {
        public int Id { get; set; }
        public long AppUserId { get; set; }
        public string Token { get; set; } = default!;
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset? ConsumedAtUtc { get; set; }
        public string? DeviceInfo { get; set; }
    }
}
