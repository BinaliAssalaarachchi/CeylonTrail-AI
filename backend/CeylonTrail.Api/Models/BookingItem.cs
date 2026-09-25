namespace CeylonTrail.Api.Models;

public class BookingItem
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid AvailabilitySlotId { get; set; }
    public AvailabilitySlot? AvailabilitySlot { get; set; }

    public int NumberOfGuests { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }
}
