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
    public int Score { get; set; }
}