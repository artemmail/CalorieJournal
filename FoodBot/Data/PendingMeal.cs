using System;
using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public class PendingMeal
{
    [Key] public int Id { get; set; }
    public long ChatId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? FileMime { get; set; }
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public int Attempts { get; set; }
}

