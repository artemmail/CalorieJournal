using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace FoodBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) {}
    public DbSet<MealEntry> Meals => Set<MealEntry>();
    public DbSet<PendingMeal> PendingMeals => Set<PendingMeal>();
    public DbSet<PendingClarify> PendingClarifies => Set<PendingClarify>();


    public DbSet<AppStartCode> StartCodes => Set<AppStartCode>();
    public DbSet<PersonalCard> PersonalCards => Set<PersonalCard>();
    public DbSet<AnalysisReport1> AnalysisReports2 => Set<AnalysisReport1>();
    public DbSet<PeriodPdfJob> PeriodPdfJobs => Set<PeriodPdfJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MealEntry>()
         .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });

        modelBuilder.Entity<PendingMeal>()
            .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });

        modelBuilder.Entity<PendingClarify>()
            .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });

        modelBuilder.Entity<AppStartCode>()
        .HasIndex(x => x.Code)
        .IsUnique();

        modelBuilder.Entity<AppStartCode>()
            .HasIndex(x => new { x.ChatId, x.ExpiresAtUtc });

        modelBuilder.Entity<PersonalCard>()
            .HasKey(x => x.ChatId);

        modelBuilder.Entity<PersonalCard>()
            .Property(x => x.ChatId)
            .ValueGeneratedNever();

        modelBuilder.Entity<PeriodPdfJob>()
            .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });

       
    }
}
