namespace AllFlight.Services;

public class TransparencyFactor
{
    public string Label { get; set; } = "";
    public int MaxPoints { get; set; }
    public bool Disclosed { get; set; }
    public int EarnedPoints => Disclosed ? MaxPoints : 0;
}

public static class TransparencyScoreCalculator
{
    public static List<TransparencyFactor> GetBreakdown(Flight flight)
    {
        return new List<TransparencyFactor>
        {
            new() { Label = "Base Fare Visible", MaxPoints = 20, Disclosed = flight.BaseFare.HasValue },
            new() { Label = "Taxes Visible", MaxPoints = 20, Disclosed = flight.TaxesAndFees.HasValue },
            new() { Label = "Baggage Visible", MaxPoints = 20, Disclosed = !string.IsNullOrWhiteSpace(flight.BaggagePolicy) },
            new() { Label = "Seat Fees Visible", MaxPoints = 20, Disclosed = flight.SeatSelectionIncluded.HasValue },
            new() { Label = "Cancellation Policy", MaxPoints = 12, Disclosed = !string.IsNullOrWhiteSpace(flight.CancellationPolicy) },
            new() { Label = "Change Policy", MaxPoints = 8, Disclosed = flight.ChangeFee.HasValue },
        };
    }

    public static int Calculate(Flight flight)
    {
        return GetBreakdown(flight).Sum(f => f.EarnedPoints);
    }
}