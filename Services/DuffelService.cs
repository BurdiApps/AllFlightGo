using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AllFlight.Services;

// A worldwide airport/city suggestion from Duffel's places API.
public record PlaceSuggestion(string IataCode, string Name, string? CityName, string Type)
{
    // Friendly label shown in the autocomplete list, e.g. "New York — John F. Kennedy (JFK)".
    public string Display => Type == "airport" && !string.IsNullOrWhiteSpace(CityName)
        ? $"{CityName} — {Name} ({IataCode})"
        : $"{Name} ({IataCode})";
}

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

            // --- Transparency data ---
            // Each field is only "disclosed" (and thus scored) when Duffel actually
            // returns it. Airlines that share less stay null and score lower — which
            // is exactly what the Transparency Score is meant to surface.

            // Baggage allowance, read from the first segment's first passenger.
            string? baggagePolicy = null;
            if (first.TryGetProperty("passengers", out var segPassengers)
                && segPassengers.ValueKind == JsonValueKind.Array
                && segPassengers.GetArrayLength() > 0
                && segPassengers[0].TryGetProperty("baggages", out var baggages)
                && baggages.ValueKind == JsonValueKind.Array
                && baggages.GetArrayLength() > 0)
            {
                var checkedQty = 0;
                var carryQty = 0;
                foreach (var b in baggages.EnumerateArray())
                {
                    var qty = b.TryGetProperty("quantity", out var bq) && bq.TryGetInt32(out var qv) ? qv : 0;
                    var btype = b.TryGetProperty("type", out var bt) ? bt.GetString() : null;
                    if (btype == "checked") checkedQty += qty;
                    else if (btype == "carry_on") carryQty += qty;
                }
                baggagePolicy = $"{checkedQty} checked bag(s), {carryQty} carry-on included";
            }

            // Refund / change conditions (offer-level).
            string? cancellationPolicy = null;
            decimal? changeFee = null;
            if (offer.TryGetProperty("conditions", out var conditions)
                && conditions.ValueKind == JsonValueKind.Object)
            {
                if (conditions.TryGetProperty("refund_before_departure", out var refund)
                    && refund.ValueKind == JsonValueKind.Object)
                {
                    var allowed = refund.TryGetProperty("allowed", out var ra)
                                  && ra.ValueKind == JsonValueKind.True;
                    if (refund.TryGetProperty("penalty_amount", out var rp)
                        && rp.ValueKind == JsonValueKind.String
                        && decimal.TryParse(rp.GetString(), out var penalty))
                    {
                        cancellationPolicy = allowed ? $"Refundable (−{Money.Usd(penalty)} penalty)" : "Non-refundable";
                    }
                    else
                    {
                        cancellationPolicy = allowed ? "Fully refundable" : "Non-refundable";
                    }
                }

                if (conditions.TryGetProperty("change_before_departure", out var change)
                    && change.ValueKind == JsonValueKind.Object)
                {
                    if (change.TryGetProperty("penalty_amount", out var cp)
                        && cp.ValueKind == JsonValueKind.String
                        && decimal.TryParse(cp.GetString(), out var cfee))
                    {
                        changeFee = cfee;
                    }
                    else if (change.TryGetProperty("allowed", out var ca)
                             && ca.ValueKind == JsonValueKind.True)
                    {
                        changeFee = 0m;
                    }
                }
            }

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
                BaggagePolicy = baggagePolicy,
                SeatSelectionIncluded = null, // determined on the seat-map step, not at search time
                CancellationPolicy = cancellationPolicy,
                ChangeFee = changeFee
            });
        }

        return flights.OrderBy(f => f.Price).ToList();
    }

    // This asks Duffel for airports/cities that match what the user typed.
    // For example, typing "lond" gives back London Heathrow, London Gatwick, etc.
    // It powers the little dropdown of suggestions in the search box.
    public async Task<List<PlaceSuggestion>> SuggestPlacesAsync(string query)
    {
        // Don't bother searching for nothing or just one letter.
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new();

        // Call Duffel's "places/suggestions" web address with the user's text.
        // Uri.EscapeDataString makes the text safe to put in a URL (spaces, etc.).
        var res = await _http.GetAsync($"places/suggestions?query={Uri.EscapeDataString(query)}");
        if (!res.IsSuccessStatusCode)
            return new(); // if Duffel had a problem, just return an empty list

        // Read the JSON that Duffel sent back so we can pull the pieces we want out of it.
        using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var list = new List<PlaceSuggestion>();

        // The results live inside a "data" array. If it isn't there, stop early.
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;

        // Go through each place Duffel found and build a simple object for it.
        foreach (var p in data.EnumerateArray())
        {
            var type = p.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

            // Grab the airport code (like "JFK"). If this is a whole city instead of a
            // single airport, use the city code instead.
            var code = p.TryGetProperty("iata_code", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : (p.TryGetProperty("iata_city_code", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString() : null);
            if (string.IsNullOrWhiteSpace(code))
                continue; // no code means we can't search with it, so skip it

            var name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var cityName = p.TryGetProperty("city_name", out var cn) && cn.ValueKind == JsonValueKind.String
                ? cn.GetString()
                : null;

            list.Add(new PlaceSuggestion(code!, name, cityName, type));
        }

        return list;
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