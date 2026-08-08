namespace AllFlight.Services;

public class SelectedFlightService
{
    public Flight? Current { get; private set; }
    public void Set(Flight flight) => Current = flight;
}