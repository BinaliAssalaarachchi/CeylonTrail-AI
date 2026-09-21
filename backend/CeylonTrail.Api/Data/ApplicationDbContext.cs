using Microsoft.EntityFrameworkCore;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<TravelAlert> TravelAlerts => Set<TravelAlert>();

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
        });

        modelBuilder.Entity<TravelAlert>(entity =>
        {
            entity.HasKey(alert => alert.Id);

            entity.Property(alert => alert.Id)
                .ValueGeneratedOnAdd();

            entity.Property(alert => alert.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(alert => alert.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(alert => alert.AlertType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(alert => alert.Severity)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(alert => alert.District)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(alert => alert.StartDateTime)
                .IsRequired();

            entity.Property(alert => alert.EndDateTime)
                .IsRequired();

            entity.Property(alert => alert.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(alert => alert.Source)
                .HasMaxLength(500);

            entity.Property(alert => alert.CreatedByUserId)
                .IsRequired();

            entity.Property(alert => alert.CreatedAt)
                .IsRequired();

            entity.Property(alert => alert.UpdatedAt)
                .IsRequired();

            entity.HasOne(alert => alert.CreatedByUser)
                .WithMany()
                .HasForeignKey(alert => alert.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(alert => new { alert.Status, alert.StartDateTime });
            entity.HasIndex(alert => new { alert.District, alert.Status, alert.StartDateTime });
        });
    }
}
