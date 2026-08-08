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

    public async Task<List<SeatMapCabin>> GetSeatMapAsync(string offerId)
{
    var res = await _http.GetAsync($"air/seat_maps?offer_id={offerId}");
    res.EnsureSuccessStatusCode();

    using var stream = await res.Content.ReadAsStreamAsync();
    using var doc = await JsonDocument.ParseAsync(stream);

    var cabins = new List<SeatMapCabin>();
    var seatMaps = doc.RootElement.GetProperty("data");

    // one seat map per segment — take the first for now
    if (seatMaps.GetArrayLength() == 0) return cabins;

    var firstMap = seatMaps[0];
    foreach (var cabinJson in firstMap.GetProperty("cabins").EnumerateArray())
    {
        var cabin = new SeatMapCabin();

        foreach (var rowJson in cabinJson.GetProperty("rows").EnumerateArray())
        {
            var row = new SeatRow();

            foreach (var sectionJson in rowJson.GetProperty("sections").EnumerateArray())
            {
                var section = new List<SeatElement>();

                foreach (var el in sectionJson.GetProperty("elements").EnumerateArray())
                {
                    var type = el.GetProperty("type").GetString() ?? "";
                    string? designator = el.TryGetProperty("designator", out var d) ? d.GetString() : null;

                    decimal? price = null;
                    string? currency = null;

                    if (el.TryGetProperty("available_services", out var services) && services.GetArrayLength() > 0)
                    {
                        var firstService = services[0];
                        price = decimal.Parse(firstService.GetProperty("total_amount").GetString() ?? "0");
                        currency = firstService.GetProperty("total_currency").GetString();
                    }

                    section.Add(new SeatElement
                    {
                        Type = type,
                        Designator = designator,
                        Price = price,
                        Currency = currency
                    });
                }

                row.Sections.Add(section);
            }

            cabin.Rows.Add(row);
        }

        cabins.Add(cabin);
    }

    return cabins;
}
}