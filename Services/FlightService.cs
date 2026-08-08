namespace AllFlight.Services;

public class FlightService
{
    private readonly DuffelService _duffel;

    public FlightService(DuffelService duffel) => _duffel = duffel;

    public async Task<List<Flight>> SearchAsync(string origin, string destination, DateTime date, int passengers)
    {
        try
        {
            return await _duffel.SearchAsync(origin, destination, date, passengers);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Duffel error: {ex.Message}");
            return new List<Flight>();
        }
    }

    public async Task<List<SeatMapCabin>> GetSeatMapAsync(string offerId)
    {
        try
        {
            return await _duffel.GetSeatMapAsync(offerId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Duffel seat map error: {ex.Message}");
            return new List<SeatMapCabin>();
        }
}
}