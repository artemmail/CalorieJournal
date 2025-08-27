using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace FoodBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) {}
    public DbSet<MealEntry> Meals => Set<MealEntry>();


    public DbSet<AppStartCode> StartCodes => Set<AppStartCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MealEntry>()
         .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });

        modelBuilder.Entity<AppStartCode>()
        .HasIndex(x => x.Code)
        .IsUnique();

        modelBuilder.Entity<AppStartCode>()
            .HasIndex(x => new { x.ChatId, x.ExpiresAtUtc });
    }
}
