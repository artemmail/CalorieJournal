using System;
using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public class PendingClarify
{
    [Key] public int Id { get; set; }
    public long AppUserId { get; set; }
    public int MealId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset? NewTime { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int Attempts { get; set; }
}
