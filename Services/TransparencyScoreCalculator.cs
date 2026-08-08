namespace AllFlight.Services;

public static class TransparencyScoreCalculator
{
    public static int Calculate(Flight flight)
    {
        var checks = new List<bool>
        {
            flight.BaseFare.HasValue,
            flight.TaxesAndFees.HasValue,
            !string.IsNullOrWhiteSpace(flight.BaggagePolicy),
            flight.SeatSelectionIncluded.HasValue,
            !string.IsNullOrWhiteSpace(flight.CancellationPolicy),
            flight.ChangeFee.HasValue
        };

        var disclosedCount = checks.Count(c => c);
        return (int)Math.Round((double)disclosedCount / checks.Count * 100);
    }
}