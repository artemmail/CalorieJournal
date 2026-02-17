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
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppIdentity> AppIdentities => Set<AppIdentity>();
    public DbSet<AppUserDevice> AppUserDevices => Set<AppUserDevice>();
    public DbSet<AppRefreshToken> AppRefreshTokens => Set<AppRefreshToken>();
    public DbSet<PersonalCard> PersonalCards => Set<PersonalCard>();
    public DbSet<AnalysisReport1> AnalysisReports2 => Set<AnalysisReport1>();
    public DbSet<PeriodPdfJob> PeriodPdfJobs => Set<PeriodPdfJob>();
    public DbSet<AnalysisPdfJob> AnalysisPdfJobs => Set<AnalysisPdfJob>();

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
        modelBuilder.Entity<AppStartCode>()
            .HasIndex(x => new { x.AppUserId, x.ExpiresAtUtc });
        modelBuilder.Entity<AppStartCode>()
            .HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AppUser>()
            .HasIndex(x => x.StorageChatId)
            .IsUnique();
        modelBuilder.Entity<AppUser>()
            .Property(x => x.Status)
            .HasDefaultValue("active");

        modelBuilder.Entity<AppIdentity>()
            .HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AppIdentity>()
            .HasIndex(x => new { x.Provider, x.ExternalUserId })
            .IsUnique();
        modelBuilder.Entity<AppIdentity>()
            .HasIndex(x => x.AppUserId);

        modelBuilder.Entity<AppUserDevice>()
            .HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AppUserDevice>()
            .HasIndex(x => x.InstallId)
            .IsUnique();
        modelBuilder.Entity<AppUserDevice>()
            .HasIndex(x => x.AppUserId);

        modelBuilder.Entity<AppRefreshToken>()
            .HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<AppRefreshToken>()
            .HasOne(x => x.AppUserDevice)
            .WithMany()
            .HasForeignKey(x => x.AppUserDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AppRefreshToken>()
            .HasIndex(x => x.TokenHash)
            .IsUnique();
        modelBuilder.Entity<AppRefreshToken>()
            .HasIndex(x => new { x.AppUserId, x.ExpiresAtUtc });

        modelBuilder.Entity<PersonalCard>()
            .HasKey(x => x.ChatId);

        modelBuilder.Entity<PersonalCard>()
            .Property(x => x.ChatId)
            .ValueGeneratedNever();

        modelBuilder.Entity<PeriodPdfJob>()
            .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });
        modelBuilder.Entity<PeriodPdfJob>()
            .Property(x => x.Format)
            .HasDefaultValue(PeriodReportFormat.Pdf);

        modelBuilder.Entity<AnalysisPdfJob>()
            .HasIndex(x => new { x.ChatId, x.CreatedAtUtc });


    }
}
