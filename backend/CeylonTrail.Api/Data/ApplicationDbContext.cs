using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Attraction> Attractions => Set<Attraction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AttractionSchedule> AttractionSchedules => Set<AttractionSchedule>();
    public DbSet<ExperienceSlot> ExperienceSlots => Set<ExperienceSlot>();
    public DbSet<AttractionImage> AttractionImages => Set<AttractionImage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).ValueGeneratedOnAdd();
            entity.Property(user => user.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(user => user.LastName).IsRequired().HasMaxLength(100);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(user => user.Role).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(user => user.IsActive).IsRequired();
            entity.Property(user => user.CreatedAt).IsRequired();
            entity.Property(user => user.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Id).ValueGeneratedOnAdd();
            entity.Property(category => category.Name).IsRequired().HasMaxLength(100);
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => category.Name).IsUnique();
        });

        modelBuilder.Entity<Attraction>(entity =>
        {
            entity.HasKey(attraction => attraction.Id);
            entity.Property(attraction => attraction.Id).ValueGeneratedOnAdd();
            entity.Property(attraction => attraction.Name).IsRequired().HasMaxLength(200);
            entity.Property(attraction => attraction.Description).IsRequired().HasMaxLength(4000);
            entity.Property(attraction => attraction.District).IsRequired().HasMaxLength(100);
            entity.Property(attraction => attraction.Address).IsRequired().HasMaxLength(300);
            entity.Property(attraction => attraction.Price).HasPrecision(12, 2).IsRequired();
            entity.Property(attraction => attraction.Status).IsRequired().HasMaxLength(30);
            entity.Property(attraction => attraction.IsActive).IsRequired();
            entity.Property(attraction => attraction.CreatedAt).IsRequired();
            entity.Property(attraction => attraction.UpdatedAt).IsRequired();
            entity.HasIndex(attraction => new { attraction.CategoryId, attraction.Status, attraction.IsActive });
            entity.HasIndex(attraction => new { attraction.ProviderId, attraction.Status });
            entity.HasIndex(attraction => attraction.Name);
            entity.ToTable(table => table.HasCheckConstraint("CK_Attractions_Price_NonNegative", "\"Price\" >= 0"));
            entity.ToTable(table => table.HasCheckConstraint("CK_Attractions_Latitude_Range", "\"Latitude\" >= -90 AND \"Latitude\" <= 90"));
            entity.ToTable(table => table.HasCheckConstraint("CK_Attractions_Longitude_Range", "\"Longitude\" >= -180 AND \"Longitude\" <= 180"));
            entity.HasOne(attraction => attraction.Provider).WithMany().HasForeignKey(attraction => attraction.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(attraction => attraction.Category).WithMany(category => category.Attractions).HasForeignKey(attraction => attraction.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttractionSchedule>(entity =>
        {
            entity.HasKey(schedule => schedule.Id);
            entity.Property(schedule => schedule.Id).ValueGeneratedOnAdd();
            entity.Property(schedule => schedule.DayOfWeek).HasConversion<string>().IsRequired().HasMaxLength(10);
            entity.Property(schedule => schedule.IsClosed).IsRequired();
            entity.HasIndex(schedule => new { schedule.AttractionId, schedule.DayOfWeek }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_AttractionSchedules_TimeRange", "\"IsClosed\" OR (\"OpeningTime\" IS NOT NULL AND \"ClosingTime\" IS NOT NULL AND \"OpeningTime\" < \"ClosingTime\")"));
            entity.HasOne(schedule => schedule.Attraction).WithMany(attraction => attraction.Schedules).HasForeignKey(schedule => schedule.AttractionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExperienceSlot>(entity =>
        {
            entity.HasKey(slot => slot.Id);
            entity.Property(slot => slot.Id).ValueGeneratedOnAdd();
            entity.Property(slot => slot.Date).IsRequired();
            entity.Property(slot => slot.StartTime).IsRequired();
            entity.Property(slot => slot.EndTime).IsRequired();
            entity.Property(slot => slot.Capacity).IsRequired();
            entity.Property(slot => slot.AvailableCapacity).IsRequired();
            entity.HasIndex(slot => new { slot.AttractionId, slot.Date, slot.StartTime }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_ExperienceSlots_TimeRange", "\"StartTime\" < \"EndTime\""));
            entity.ToTable(table => table.HasCheckConstraint("CK_ExperienceSlots_Capacity_Positive", "\"Capacity\" > 0"));
            entity.ToTable(table => table.HasCheckConstraint("CK_ExperienceSlots_AvailableCapacity_Valid", "\"AvailableCapacity\" >= 0 AND \"AvailableCapacity\" <= \"Capacity\""));
            entity.HasOne(slot => slot.Attraction).WithMany(attraction => attraction.ExperienceSlots).HasForeignKey(slot => slot.AttractionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttractionImage>(entity =>
        {
            entity.HasKey(image => image.Id);
            entity.Property(image => image.Id).ValueGeneratedOnAdd();
            entity.Property(image => image.ImageUrl).IsRequired().HasMaxLength(2048);
            entity.Property(image => image.AltText).HasMaxLength(300);
            entity.Property(image => image.SortOrder).IsRequired();
            entity.Property(image => image.CreatedAt).IsRequired();
            entity.HasIndex(image => new { image.AttractionId, image.SortOrder });
            entity.HasOne(image => image.Attraction)
                .WithMany(attraction => attraction.Images)
                .HasForeignKey(image => image.AttractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(favorite => favorite.Id);
            entity.Property(favorite => favorite.Id).ValueGeneratedOnAdd();
            entity.Property(favorite => favorite.CreatedAt).IsRequired();
            entity.HasIndex(favorite => new { favorite.TouristId, favorite.AttractionId }).IsUnique();
            entity.HasOne(favorite => favorite.Tourist).WithMany().HasForeignKey(favorite => favorite.TouristId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(favorite => favorite.Attraction).WithMany(attraction => attraction.Favorites).HasForeignKey(favorite => favorite.AttractionId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
