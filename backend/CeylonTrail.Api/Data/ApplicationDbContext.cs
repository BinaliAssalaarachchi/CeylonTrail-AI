using Microsoft.EntityFrameworkCore;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Cancellation> Cancellations => Set<Cancellation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. User Entity Configuration
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

        // 2. Booking Entity Configuration
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(b => b.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(b => b.CreatedAt)
                .IsRequired();

            entity.Property(b => b.UpdatedAt)
                .IsRequired();

            // Indexes for fast lookup
            entity.HasIndex(b => b.TouristId);
            entity.HasIndex(b => b.Status);
            entity.HasIndex(b => b.CreatedAt);

            // Relationships
            entity.HasOne(b => b.Tourist)
                .WithMany()
                .HasForeignKey(b => b.TouristId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(b => b.Items)
                .WithOne(i => i.Booking)
                .HasForeignKey(i => i.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(b => b.StatusHistory)
                .WithOne(h => h.Booking)
                .HasForeignKey(h => h.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Cancellation)
                .WithOne(c => c.Booking)
                .HasForeignKey<Cancellation>(c => c.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. BookingItem Entity Configuration
        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Quantity)
                .IsRequired();

            entity.Property(item => item.UnitPrice)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(item => item.Subtotal)
                .HasPrecision(18, 2)
                .IsRequired();
        });

        // 4. BookingStatusHistory Entity Configuration
        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.HasKey(h => h.Id);

            entity.Property(h => h.PreviousStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(h => h.NewStatus)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(h => h.Reason)
                .HasMaxLength(500);

            entity.Property(h => h.ChangedAt)
                .IsRequired();
        });

        // 5. Cancellation Entity Configuration
        modelBuilder.Entity<Cancellation>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Reason)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(c => c.CancelledAt)
                .IsRequired();
        });
    }
}
