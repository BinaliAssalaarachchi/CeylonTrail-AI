using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CeylonTrail.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookingModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"BookingStatusHistories\"; DELETE FROM \"BookingItems\"; DELETE FROM \"Cancellations\"; DELETE FROM \"Bookings\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_TouristId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "Cancellations");

            migrationBuilder.DropIndex(
                name: "IX_BookingStatusHistories_BookingId",
                table: "BookingStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CreatedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ChangedBy",
                table: "BookingStatusHistories");

            migrationBuilder.RenameColumn(
                name: "ChangedAt",
                table: "BookingStatusHistories",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "TouristId",
                table: "Bookings",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Bookings",
                newName: "CurrentStatus");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_TouristId",
                table: "Bookings",
                newName: "IX_Bookings_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                newName: "IX_Bookings_CurrentStatus");

            migrationBuilder.RenameColumn(
                name: "Subtotal",
                table: "BookingItems",
                newName: "SubTotal");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "BookingItems",
                newName: "NumberOfGuests");

            migrationBuilder.RenameColumn(
                name: "AttractionId",
                table: "BookingItems",
                newName: "AvailabilitySlotId");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BookingStatusHistories",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChangedByUserId",
                table: "BookingStatusHistories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeHash",
                table: "Bookings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AvailabilitySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: false),
                    BookedCapacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PricePerPerson = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilitySlots", x => x.Id);
                    table.CheckConstraint("CK_AvailabilitySlot_Capacity_Valid", "\"BookedCapacity\" >= 0 AND \"BookedCapacity\" <= \"MaxCapacity\"");
                    table.CheckConstraint("CK_AvailabilitySlot_Price_NonNegative", "\"PricePerPerson\" >= 0");
                    table.CheckConstraint("CK_AvailabilitySlot_TimeRange", "\"StartTime\" < \"EndTime\"");
                    table.ForeignKey(
                        name: "FK_AvailabilitySlots_Attractions_AttractionId",
                        column: x => x.AttractionId,
                        principalTable: "Attractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CancellationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationRequests", x => x.Id);
                    table.CheckConstraint("CK_CancellationRequest_RefundAmount_NonNegative", "\"RefundAmount\" IS NULL OR \"RefundAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CancellationRequests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CancellationRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_BookingId_Timestamp",
                table: "BookingStatusHistories",
                columns: new[] { "BookingId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_ChangedByUserId",
                table: "BookingStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_QrCodeHash",
                table: "Bookings",
                column: "QrCodeHash",
                unique: true,
                filter: "\"QrCodeHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TripId",
                table: "Bookings",
                column: "TripId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booking_TotalAmount_NonNegative",
                table: "Bookings",
                sql: "\"TotalAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingItems_AvailabilitySlotId",
                table: "BookingItems",
                column: "AvailabilitySlotId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingItem_Guests_Positive",
                table: "BookingItems",
                sql: "\"NumberOfGuests\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingItem_SubTotal_NonNegative",
                table: "BookingItems",
                sql: "\"SubTotal\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingItem_UnitPrice_NonNegative",
                table: "BookingItems",
                sql: "\"UnitPrice\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_AttractionId_StartTime_EndTime",
                table: "AvailabilitySlots",
                columns: new[] { "AttractionId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_BookingId_Status",
                table: "CancellationRequests",
                columns: new[] { "BookingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_ReviewedByUserId",
                table: "CancellationRequests",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingItems_AvailabilitySlots_AvailabilitySlotId",
                table: "BookingItems",
                column: "AvailabilitySlotId",
                principalTable: "AvailabilitySlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingStatusHistories_Users_ChangedByUserId",
                table: "BookingStatusHistories",
                column: "ChangedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingItems_AvailabilitySlots_AvailabilitySlotId",
                table: "BookingItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Trips_TripId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingStatusHistories_Users_ChangedByUserId",
                table: "BookingStatusHistories");

            migrationBuilder.DropTable(
                name: "AvailabilitySlots");

            migrationBuilder.DropTable(
                name: "CancellationRequests");

            migrationBuilder.DropIndex(
                name: "IX_BookingStatusHistories_BookingId_Timestamp",
                table: "BookingStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_BookingStatusHistories_ChangedByUserId",
                table: "BookingStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_QrCodeHash",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TripId",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Booking_TotalAmount_NonNegative",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_BookingItems_AvailabilitySlotId",
                table: "BookingItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingItem_Guests_Positive",
                table: "BookingItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingItem_SubTotal_NonNegative",
                table: "BookingItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingItem_UnitPrice_NonNegative",
                table: "BookingItems");

            migrationBuilder.DropColumn(
                name: "ChangedByUserId",
                table: "BookingStatusHistories");

            migrationBuilder.DropColumn(
                name: "QrCodeHash",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "BookingStatusHistories",
                newName: "ChangedAt");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Bookings",
                newName: "TouristId");

            migrationBuilder.RenameColumn(
                name: "CurrentStatus",
                table: "Bookings",
                newName: "Status");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                newName: "IX_Bookings_TouristId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_CurrentStatus",
                table: "Bookings",
                newName: "IX_Bookings_Status");

            migrationBuilder.RenameColumn(
                name: "SubTotal",
                table: "BookingItems",
                newName: "Subtotal");

            migrationBuilder.RenameColumn(
                name: "NumberOfGuests",
                table: "BookingItems",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "AvailabilitySlotId",
                table: "BookingItems",
                newName: "AttractionId");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BookingStatusHistories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChangedBy",
                table: "BookingStatusHistories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Cancellations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cancellations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cancellations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistories_BookingId",
                table: "BookingStatusHistories",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CreatedAt",
                table: "Bookings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cancellations_BookingId",
                table: "Cancellations",
                column: "BookingId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_TouristId",
                table: "Bookings",
                column: "TouristId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
