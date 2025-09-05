using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

public class MealEntry
{
    [Key] public int Id { get; set; }

    // Telegram
    public long ChatId { get; set; }
    public long UserId { get; set; }
    [MaxLength(256)] public string? Username { get; set; }

    // When saved
    public DateTimeOffset CreatedAtUtc { get; set; }

    // Image
    public string FileId { get; set; } = null!;
    public string? FileMime { get; set; }
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

    // Extracted nutrition
    [MaxLength(256)] public string? DishName { get; set; }
    public string? IngredientsJson { get; set; }
    public string? ProductsJson { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal? WeightG { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal? ProteinsG { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal? FatsG { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal? CarbsG { get; set; }
    [Column(TypeName="decimal(10,2)")] public decimal? CaloriesKcal { get; set; }
    [MaxLength(64)] public string? Model { get; set; }
    [Column(TypeName="decimal(5,2)")] public decimal? Confidence { get; set; }

    // NEW: snapshot шага 1 и (необяз.) последний reasoning-промпт
    public string? Step1Json { get; set; }
    public string? ReasoningPrompt { get; set; }

    // Последнее уточнение от пользователя
    public string? ClarifyNote { get; set; }
}
