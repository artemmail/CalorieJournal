using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace FoodBot.Data;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) {}
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();
    public DbSet<MealEntry> Meals => Set<MealEntry>();
    public DbSet<PendingMeal> PendingMeals => Set<PendingMeal>();
    public DbSet<PendingClarify> PendingClarifies => Set<PendingClarify>();


    public DbSet<AppStartCode> StartCodes => Set<AppStartCode>();
    public DbSet<PersonalCard> PersonalCards => Set<PersonalCard>();
    public DbSet<AnalysisReport1> AnalysisReports2 => Set<AnalysisReport1>();
    public DbSet<PeriodPdfJob> PeriodPdfJobs => Set<PeriodPdfJob>();
    public DbSet<AnalysisPdfJob> AnalysisPdfJobs => Set<AnalysisPdfJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        modelBuilder.Entity<ExternalAccount>()
            .HasIndex(x => new { x.Provider, x.ExternalId })
            .IsUnique();

        modelBuilder.Entity<ExternalAccount>()
            .Property(x => x.LinkedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        modelBuilder.Entity<MealEntry>()
         .HasIndex(x => new { x.AppUserId, x.CreatedAtUtc });

        modelBuilder.Entity<PendingMeal>()
            .HasIndex(x => new { x.AppUserId, x.CreatedAtUtc });

        modelBuilder.Entity<PendingClarify>()
            .HasIndex(x => new { x.AppUserId, x.CreatedAtUtc });

        modelBuilder.Entity<AppStartCode>()
        .HasIndex(x => x.Code)
        .IsUnique();

        modelBuilder.Entity<AppStartCode>()
            .HasIndex(x => new { x.AppUserId, x.ExpiresAtUtc });

        modelBuilder.Entity<AppStartCode>()
            .HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<AppStartCode>()
            .HasOne<ExternalAccount>()
            .WithMany()
            .HasForeignKey(x => x.ExternalAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PersonalCard>()
            .HasKey(x => x.AppUserId);

        modelBuilder.Entity<PersonalCard>()
            .Property(x => x.AppUserId)
            .ValueGeneratedNever();

        modelBuilder.Entity<PeriodPdfJob>()
            .HasIndex(x => new { x.AppUserId, x.CreatedAtUtc });
        modelBuilder.Entity<PeriodPdfJob>()
            .Property(x => x.Format)
            .HasDefaultValue(PeriodReportFormat.Pdf);

        modelBuilder.Entity<AnalysisPdfJob>()
            .HasIndex(x => new { x.AppUserId, x.CreatedAtUtc });


    }
}
