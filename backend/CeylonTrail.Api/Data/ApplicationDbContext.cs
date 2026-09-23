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
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Cancellation> Cancellations => Set<Cancellation>();
    public DbSet<TravelAlert> TravelAlerts => Set<TravelAlert>();
    public DbSet<ValidationResult> ValidationResults => Set<ValidationResult>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

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
