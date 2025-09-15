using FoodBot.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodBot.Services;

public sealed class PersonalCardService
{
    private readonly BotDbContext _db;
    public PersonalCardService(BotDbContext db) => _db = db;

    public Task<PersonalCard?> GetAsync(long chatId, CancellationToken ct = default)
        => _db.PersonalCards.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == chatId, ct);

    public async Task<PersonalCard> UpsertAsync(long chatId, PersonalCard card, CancellationToken ct = default)
    {
        var existing = await _db.PersonalCards.FirstOrDefaultAsync(x => x.ChatId == chatId, ct);
        if (existing == null)
        {
            card.ChatId = chatId;
            RecalcDailyCalories(card);
            _db.PersonalCards.Add(card);
            await _db.SaveChangesAsync(ct);
            return card;
        }

        existing.Email = card.Email;
        existing.Name = card.Name;
        existing.BirthYear = card.BirthYear;
        existing.Gender = card.Gender;
        existing.HeightCm = card.HeightCm;
        existing.WeightKg = card.WeightKg;
        existing.ActivityLevel = card.ActivityLevel;
        existing.DietGoals = card.DietGoals;
        existing.MedicalRestrictions = card.MedicalRestrictions;
        RecalcDailyCalories(existing);
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    private static void RecalcDailyCalories(PersonalCard card)
    {
        if (card.Gender.HasValue && card.HeightCm.HasValue && card.WeightKg.HasValue &&
            card.BirthYear.HasValue && card.ActivityLevel.HasValue)
        {
            var age = DateTime.UtcNow.Year - card.BirthYear.Value;
            var weight = card.WeightKg.Value;
            var height = card.HeightCm.Value;
            decimal bmr = card.Gender == Gender.Male
                ? 10m * weight + 6.25m * height - 5m * age + 5m
                : 10m * weight + 6.25m * height - 5m * age - 161m;
            decimal pal = card.ActivityLevel switch
            {
                ActivityLevel.Minimal => 1.2m,
                ActivityLevel.Light => 1.375m,
                ActivityLevel.Moderate => 1.55m,
                ActivityLevel.High => 1.725m,
                ActivityLevel.VeryHigh => 1.9m,
                _ => 1m
            };
            card.DailyCalories = (int)Math.Round(bmr * pal);
        }
        else
        {
            card.DailyCalories = null;
        }
    }
}
