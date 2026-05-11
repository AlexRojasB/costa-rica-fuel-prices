using CRFuelScraper.Core.Entities;
using CRFuelScraper.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CRFuelScraper.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FuelPrice>    FuelPrices        => Set<FuelPrice>();
    public DbSet<ScraperLog>   ScraperLogs       => Set<ScraperLog>();
    public DbSet<SourceHealth> SourceHealthItems => Set<SourceHealth>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FuelPrice>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Price).HasColumnType("decimal(10,4)");
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.Source).HasMaxLength(200);
            e.Property(x => x.SourceUrl).HasMaxLength(500);
            e.Property(x => x.FuelType).HasConversion<string>().HasMaxLength(20);

            e.Property(x => x.ConfidenceScore).HasColumnType("decimal(3,2)").HasDefaultValue(1.0m);
            e.Property(x => x.IsStale).HasDefaultValue(false);
            e.Property(x => x.SourcePriority).HasDefaultValue(1);
            e.Property(x => x.ContentHash).HasMaxLength(64);

            e.Property(x => x.CanonicalCode).HasMaxLength(20).HasDefaultValue(string.Empty);
            e.Property(x => x.SourceProductId).HasMaxLength(50);
            e.Property(x => x.SourceProductName).HasMaxLength(200);
            e.Property(x => x.PriceWithoutTax).HasColumnType("decimal(10,4)");
            e.Property(x => x.Tax).HasColumnType("decimal(10,4)");
            e.Property(x => x.AverageMargin).HasColumnType("decimal(10,4)");
            e.Property(x => x.HasConflict).HasDefaultValue(false);
            e.Property(x => x.ConflictNote).HasMaxLength(500);

            e.HasIndex(x => new { x.FuelType, x.EffectiveDate })
                .IsUnique()
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("IX_FuelPrices_FuelType_EffectiveDate_Active");
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.EffectiveDate);
            e.HasIndex(x => x.CanonicalCode);
            e.HasIndex(x => x.ContentHash).IsUnique().HasFilter("\"ContentHash\" IS NOT NULL");
        });

        modelBuilder.Entity<ScraperLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DataSource).HasMaxLength(100);
            e.Property(x => x.ConflictsFound).HasDefaultValue(0);
            e.HasIndex(x => x.StartedAt);
        });

        modelBuilder.Entity<SourceHealth>(e =>
        {
            e.HasKey(x => x.Id);
            e.ToTable("source_health");
            e.Property(x => x.SourceName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status)
                .HasConversion(new EnumToStringConverter<SourceStatus>())
                .HasMaxLength(20);
            e.Property(x => x.ErrorMessage).HasMaxLength(500);
            e.Property(x => x.ConsecutiveFailures).HasDefaultValue(0);
            e.HasIndex(x => new { x.SourceName, x.CheckedAt });
        });
    }
}
