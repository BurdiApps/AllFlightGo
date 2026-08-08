namespace AllFlight.Services;

public class SelectedFlightService
{
    public Flight? Current { get; private set; }
    public string? SelectedSeatDesignator { get; private set; }
    public decimal? SelectedSeatPrice { get; private set; }

    public void Set(Flight flight) => Current = flight;

    public void SetSeat(string? designator, decimal? price)
    {
        SelectedSeatDesignator = designator;
        SelectedSeatPrice = price;
    }
}