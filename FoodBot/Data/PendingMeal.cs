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
    public string? Description { get; set; }
    public bool GenerateImage { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? DesiredMealTimeUtc { get; set; }
    public string? ClarifyNote { get; set; }
}

