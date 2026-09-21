using Microsoft.EntityFrameworkCore;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<TripPreference> TripPreferences => Set<TripPreference>();

    public DbSet<Itinerary> Itineraries => Set<Itinerary>();

    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();

    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id)
                .ValueGeneratedOnAdd();

            entity.Property(user => user.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(user => user.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(user => user.Email)
                .IsRequired()
                .HasMaxLength(320);

            entity.HasIndex(user => user.Email)
                .IsUnique();

            entity.Property(user => user.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(user => user.Role)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(user => user.IsActive)
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .IsRequired();

            entity.Property(user => user.UpdatedAt)
                .IsRequired();

            entity.HasMany(user => user.Trips)
                .WithOne(trip => trip.Tourist)
                .HasForeignKey(trip => trip.TouristId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(trip => trip.Id);

            entity.Property(trip => trip.Id)
                .ValueGeneratedOnAdd();

            entity.Property(trip => trip.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(trip => trip.StartDate)
                .IsRequired();

            entity.Property(trip => trip.EndDate)
                .IsRequired();

            entity.Property(trip => trip.Budget)
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(trip => trip.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(trip => trip.CreatedAt)
                .IsRequired();

            entity.Property(trip => trip.UpdatedAt)
                .IsRequired();

            entity.ToTable(table => table.HasCheckConstraint("CK_Trips_Budget_NonNegative", "\"Budget\" >= 0"));
        });

        modelBuilder.Entity<TripPreference>(entity =>
        {
            entity.HasKey(preference => preference.Id);

            entity.Property(preference => preference.Id)
                .ValueGeneratedOnAdd();

            entity.Property(preference => preference.PreferenceType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(preference => preference.Value)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasOne(preference => preference.Trip)
                .WithMany(trip => trip.Preferences)
                .HasForeignKey(preference => preference.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Itinerary>(entity =>
        {
            entity.HasKey(itinerary => itinerary.Id);

            entity.Property(itinerary => itinerary.Id)
                .ValueGeneratedOnAdd();

            entity.Property(itinerary => itinerary.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(itinerary => itinerary.TotalEstimatedCost)
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(itinerary => itinerary.CreatedAt)
                .IsRequired();

            entity.Property(itinerary => itinerary.UpdatedAt)
                .IsRequired();

            entity.ToTable(table => table.HasCheckConstraint("CK_Itineraries_TotalEstimatedCost_NonNegative", "\"TotalEstimatedCost\" >= 0"));

            entity.HasOne(itinerary => itinerary.Trip)
                .WithMany(trip => trip.Itineraries)
                .HasForeignKey(itinerary => itinerary.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItineraryDay>(entity =>
        {
            entity.HasKey(day => day.Id);

            entity.Property(day => day.Id)
                .ValueGeneratedOnAdd();

            entity.Property(day => day.DayNumber)
                .IsRequired();

            entity.Property(day => day.Date)
                .IsRequired();

            entity.HasIndex(day => new { day.ItineraryId, day.DayNumber })
                .IsUnique();

            entity.ToTable(table => table.HasCheckConstraint("CK_ItineraryDays_DayNumber_Positive", "\"DayNumber\" > 0"));

            entity.HasOne(day => day.Itinerary)
                .WithMany(itinerary => itinerary.Days)
                .HasForeignKey(day => day.ItineraryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItineraryItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id)
                .ValueGeneratedOnAdd();

            entity.Property(item => item.AttractionId)
                .IsRequired();

            entity.Property(item => item.StartTime)
                .IsRequired();

            entity.Property(item => item.EndTime)
                .IsRequired();

            entity.Property(item => item.EstimatedCost)
                .HasPrecision(12, 2)
                .IsRequired();

            entity.Property(item => item.Notes)
                .HasMaxLength(2000);

            entity.ToTable(table => table.HasCheckConstraint("CK_ItineraryItems_EstimatedCost_NonNegative", "\"EstimatedCost\" >= 0"));

            entity.HasOne(item => item.ItineraryDay)
                .WithMany(day => day.Items)
                .HasForeignKey(item => item.ItineraryDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
