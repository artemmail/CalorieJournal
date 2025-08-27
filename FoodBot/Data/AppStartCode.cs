using System;

namespace FoodBot.Data
{
    public class AppStartCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;      // короткий код (например, 8-10 символов)
        public long? ChatId { get; set; }                 // появится после ввода кода в боте
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public DateTimeOffset? ConsumedAtUtc { get; set; } // становится != null после успешного обмена на JWT
    }
}
