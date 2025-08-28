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
            _db.PersonalCards.Add(card);
            await _db.SaveChangesAsync(ct);
            return card;
        }

        existing.Email = card.Email;
        existing.Name = card.Name;
        existing.BirthYear = card.BirthYear;
        existing.DietGoals = card.DietGoals;
        existing.MedicalRestrictions = card.MedicalRestrictions;
        await _db.SaveChangesAsync(ct);
        return existing;
    }
}
