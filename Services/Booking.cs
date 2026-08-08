namespace AllFlight.Services;

public class Booking
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string ConfirmationCode { get; set; } = "";
    public string FlightId { get; set; } = "";
    public string Airline { get; set; } = "";
    public string FlightNumber { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public decimal BaseFare { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal Price { get; set; }
    public string? SeatDesignator { get; set; }
    public decimal? SeatPrice { get; set; }
    public string PassengerName { get; set; } = "";
    public string PassengerEmail { get; set; } = "";
    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
    public bool IsCancelled { get; set; }

    public string Status =>
        IsCancelled ? "Cancelled" :
        Departure < DateTime.Now ? "Completed" :
        "Upcoming";
}