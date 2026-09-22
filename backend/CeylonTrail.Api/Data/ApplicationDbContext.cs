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
    public DbSet<TravelAlert> TravelAlerts => Set<TravelAlert>();
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

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

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(booking => booking.Id);
            entity.Property(booking => booking.Id).ValueGeneratedOnAdd();
            entity.Property(booking => booking.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(booking => booking.TotalAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(booking => booking.CreatedAt).IsRequired();
            entity.Property(booking => booking.UpdatedAt).IsRequired();
            entity.HasIndex(booking => booking.TouristId);
            entity.HasIndex(booking => booking.Status);
            entity.HasIndex(booking => booking.CreatedAt);
            entity.HasOne(booking => booking.Tourist).WithMany().HasForeignKey(booking => booking.TouristId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(booking => booking.Items).WithOne(item => item.Booking).HasForeignKey(item => item.BookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(booking => booking.StatusHistory).WithOne(history => history.Booking).HasForeignKey(history => history.BookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(booking => booking.Cancellation).WithOne(cancellation => cancellation.Booking).HasForeignKey<Cancellation>(cancellation => cancellation.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookingItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.Quantity).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(item => item.Subtotal).HasPrecision(18, 2).IsRequired();
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).ValueGeneratedOnAdd();
            entity.Property(history => history.PreviousStatus).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(history => history.NewStatus).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(history => history.Reason).HasMaxLength(500);
            entity.Property(history => history.ChangedAt).IsRequired();
        });

        modelBuilder.Entity<Cancellation>(entity =>
        {
            entity.HasKey(cancellation => cancellation.Id);
            entity.Property(cancellation => cancellation.Id).ValueGeneratedOnAdd();
            entity.Property(cancellation => cancellation.Reason).IsRequired().HasMaxLength(500);
            entity.Property(cancellation => cancellation.CancelledAt).IsRequired();
        });

        modelBuilder.Entity<TravelAlert>(entity =>
        {
            entity.HasKey(alert => alert.Id);
            entity.Property(alert => alert.Id).ValueGeneratedOnAdd();
            entity.Property(alert => alert.Title).IsRequired().HasMaxLength(200);
            entity.Property(alert => alert.Description).IsRequired().HasMaxLength(2000);
            entity.Property(alert => alert.AlertType).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(alert => alert.Severity).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(alert => alert.District).IsRequired().HasMaxLength(100);
            entity.Property(alert => alert.StartDateTime).IsRequired();
            entity.Property(alert => alert.EndDateTime).IsRequired();
            entity.Property(alert => alert.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(alert => alert.Source).HasMaxLength(500);
            entity.Property(alert => alert.CreatedByUserId).IsRequired();
            entity.Property(alert => alert.CreatedAt).IsRequired();
            entity.Property(alert => alert.UpdatedAt).IsRequired();
            entity.HasOne(alert => alert.CreatedByUser).WithMany().HasForeignKey(alert => alert.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(alert => new { alert.Status, alert.StartDateTime });
            entity.HasIndex(alert => new { alert.District, alert.Status, alert.StartDateTime });
        });

        modelBuilder.Entity<ValidationResult>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.Property(result => result.Id).ValueGeneratedOnAdd();
            entity.Property(result => result.TripReference).HasMaxLength(200);
            entity.Property(result => result.OverallStatus).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(result => result.RiskLevel).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(result => result.IsFeasible).IsRequired();
            entity.Property(result => result.TotalIssueCount).IsRequired();
            entity.Property(result => result.BlockingIssueCount).IsRequired();
            entity.Property(result => result.CreatedByUserId).IsRequired();
            entity.Property(result => result.CreatedAt).IsRequired();
            entity.HasIndex(result => result.CreatedByUserId);
            entity.HasIndex(result => result.CreatedAt);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(result => result.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(result => result.Issues)
                .WithOne(issue => issue.ValidationResult)
                .HasForeignKey(issue => issue.ValidationResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValidationIssue>(entity =>
        {
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.Id).ValueGeneratedOnAdd();
            entity.Property(issue => issue.IssueType).HasConversion<string>().IsRequired().HasMaxLength(30);
            entity.Property(issue => issue.Severity).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(issue => issue.Message).IsRequired().HasMaxLength(1000);
            entity.Property(issue => issue.RuleCode).IsRequired().HasMaxLength(100);
            entity.Property(issue => issue.IsBlocking).IsRequired();
            entity.Property(issue => issue.RelatedDistrict).HasMaxLength(100);
            entity.Property(issue => issue.RelatedItemReference).HasMaxLength(200);
            entity.Property(issue => issue.CreatedAt).IsRequired();
            entity.HasIndex(issue => issue.ValidationResultId);
            entity.HasIndex(issue => new { issue.IssueType, issue.Severity });
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Id).ValueGeneratedOnAdd();
            entity.Property(request => request.Status).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(request => request.RecommendedAction).HasConversion<string>().IsRequired().HasMaxLength(40);
            entity.Property(request => request.RiskLevel).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(request => request.Summary).IsRequired().HasMaxLength(2000);
            entity.Property(request => request.AffectedItemReferences).HasMaxLength(2000);
            entity.Property(request => request.RequestedByUserId).IsRequired();
            entity.Property(request => request.ValidationResultId).IsRequired();
            entity.Property(request => request.CreatedAt).IsRequired();
            entity.Property(request => request.UpdatedAt).IsRequired();
            entity.HasIndex(request => new { request.Status, request.CreatedAt });
            entity.HasIndex(request => request.ValidationResultId);
            entity.HasOne(request => request.ValidationResult)
                .WithMany(validation => validation.ApprovalRequests)
                .HasForeignKey(request => request.ValidationResultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(request => request.RequestedByUser)
                .WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalDecision>(entity =>
        {
            entity.HasKey(decision => decision.Id);
            entity.Property(decision => decision.Id).ValueGeneratedOnAdd();
            entity.Property(decision => decision.Decision).HasConversion<string>().IsRequired().HasMaxLength(20);
            entity.Property(decision => decision.Comment).HasMaxLength(1000);
            entity.Property(decision => decision.DecidedByUserId).IsRequired();
            entity.Property(decision => decision.DecidedAt).IsRequired();
            entity.HasIndex(decision => decision.ApprovalRequestId).IsUnique();
            entity.HasIndex(decision => decision.DecidedByUserId);
            entity.HasOne(decision => decision.ApprovalRequest)
                .WithOne(request => request.Decision)
                .HasForeignKey<ApprovalDecision>(decision => decision.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(decision => decision.DecidedByUser)
                .WithMany()
                .HasForeignKey(decision => decision.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
