using System.ComponentModel.DataAnnotations;

namespace FoodBot.Data;

public class PersonalCard
{
    [Key] public long ChatId { get; set; }
    [MaxLength(256)] public string? Email { get; set; }
    [MaxLength(256)] public string? Name { get; set; }
    public int? BirthYear { get; set; }
    [MaxLength(1024)] public string? DietGoals { get; set; }
    [MaxLength(1024)] public string? MedicalRestrictions { get; set; }
}
