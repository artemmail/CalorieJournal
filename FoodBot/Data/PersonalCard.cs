using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodBot.Data;

public enum Gender
{
    Male,
    Female
}

public enum ActivityLevel
{
    Minimal,
    Light,
    Moderate,
    High,
    VeryHigh
}

public class PersonalCard
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long AppUserId { get; set; }

    [MaxLength(256)] public string? Email { get; set; }
    [MaxLength(256)] public string? Name { get; set; }
    public int? BirthYear { get; set; }

    public Gender? Gender { get; set; }
    public int? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public ActivityLevel? ActivityLevel { get; set; }
    public int? DailyCalories { get; set; }

    [MaxLength(1024)] public string? DietGoals { get; set; }
    [MaxLength(1024)] public string? MedicalRestrictions { get; set; }
}
