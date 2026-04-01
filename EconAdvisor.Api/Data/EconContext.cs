using EconAdvisor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EconAdvisor.Api.Data;

public class EconContext(DbContextOptions<EconContext> options) : DbContext(options)
{
    public DbSet<IndicatorSeries> IndicatorSeries => Set<IndicatorSeries>();
    public DbSet<IndicatorObservation> IndicatorObservations => Set<IndicatorObservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IndicatorSeries>(e =>
        {
            e.ToTable("indicator_series");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
            e.Property(x => x.SeriesKey).HasColumnName("series_key").HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(x => x.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
            e.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(30).IsRequired();

            e.HasIndex(x => new { x.CountryCode, x.SeriesKey }).IsUnique();
        });

        modelBuilder.Entity<IndicatorObservation>(e =>
        {
            e.ToTable("indicator_observations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.SeriesId).HasColumnName("series_id");
            e.Property(x => x.ObservationDate).HasColumnName("observation_date");
            e.Property(x => x.Value).HasColumnName("value").HasColumnType("numeric(18,6)");
            e.Property(x => x.FetchedAt).HasColumnName("fetched_at");

            e.HasOne(x => x.Series)
             .WithMany(x => x.Observations)
             .HasForeignKey(x => x.SeriesId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.SeriesId, x.ObservationDate }).IsUnique();
            e.HasIndex(x => x.FetchedAt);
        });
    }
}
