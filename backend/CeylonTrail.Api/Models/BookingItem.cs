namespace CeylonTrail.Api.Models;

public class BookingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Link to the parent booking
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    // Target attraction or experience slot
    public Guid AttractionId { get; set; }

    // Number of persons / tickets
    public int Quantity { get; set; }

    // Price per unit
    public decimal UnitPrice { get; set; }

    // Computed subtotal: Quantity * UnitPrice
    public decimal Subtotal { get; set; }
}
