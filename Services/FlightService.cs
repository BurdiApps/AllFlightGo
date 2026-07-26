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
}