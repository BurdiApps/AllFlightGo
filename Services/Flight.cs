namespace AllFlight.Services;

public class Flight
{
    public string Id { get; set; } = "";
    public string Airline { get; set; } = "";
    public string FlightNumber { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public int Stops { get; set; }
    public decimal Price { get; set; }

    // Flight Details breakdown fields
    public decimal? BaseFare { get; set; }
    public decimal? TaxesAndFees { get; set; }
    public string? BaggagePolicy { get; set; }
    public bool? SeatSelectionIncluded { get; set; }
    public string? CancellationPolicy { get; set; }
    public decimal? ChangeFee { get; set; }

    public TimeSpan Duration => Arrival - Departure;

    // Replaces the old settable "Score" — now calculated from disclosed fields
    public int Score => TransparencyScoreCalculator.Calculate(this);
}