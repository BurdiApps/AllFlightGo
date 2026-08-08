using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AllFlight.Services;

public class DuffelService
{
    private readonly HttpClient _http;

    public DuffelService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.duffel.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config["Duffel:AccessToken"]);
        _http.DefaultRequestHeaders.Add("Duffel-Version", "v2");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<Flight>> SearchAsync(string origin, string destination, DateTime date, int passengers)
    {
        var payload = new
        {
            data = new
            {
                slices = new[] {
                    new { origin, destination, departure_date = date.ToString("yyyy-MM-dd") }
                },
                passengers = Enumerable.Range(0, passengers).Select(_ => new { type = "adult" }).ToArray(),
                cabin_class = "economy"
            }
        };

        var res = await _http.PostAsJsonAsync("air/offer_requests?return_offers=true", payload);
        res.EnsureSuccessStatusCode();

        using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var offers = doc.RootElement.GetProperty("data").GetProperty("offers");
        var flights = new List<Flight>();

        foreach (var offer in offers.EnumerateArray())
        {
            var slice = offer.GetProperty("slices")[0];
            var segments = slice.GetProperty("segments");
            var first = segments[0];
            var last = segments[segments.GetArrayLength() - 1];

            flights.Add(new Flight
            {
                Id = offer.GetProperty("id").GetString() ?? "",
                Airline = first.GetProperty("marketing_carrier").GetProperty("name").GetString() ?? "",
                FlightNumber = first.GetProperty("marketing_carrier").GetProperty("iata_code").GetString()
                             + first.GetProperty("marketing_carrier_flight_number").GetString(),
                Origin = origin,
                Destination = destination,
                Departure = first.GetProperty("departing_at").GetDateTime(),
                Arrival = last.GetProperty("arriving_at").GetDateTime(),
                Stops = segments.GetArrayLength() - 1,
                Price = decimal.Parse(offer.GetProperty("total_amount").GetString() ?? "0"),
                BaseFare = decimal.Parse(offer.GetProperty("base_amount").GetString() ?? "0"),
                TaxesAndFees = decimal.Parse(offer.GetProperty("tax_amount").GetString() ?? "0"),
                // TODO: map real baggage/cancellation data once you confirm Duffel's
                // sandbox response includes `conditions` / `passengers[].baggages`
                BaggagePolicy = null,
                SeatSelectionIncluded = null,
                CancellationPolicy = null,
                ChangeFee = null
            });
        }

        return flights.OrderBy(f => f.Price).ToList();
    }
}