using System.Globalization;

namespace AllFlight.Services;

// Formats money as US dollars no matter what culture the server runs under.
// On the live host the container has no locale set, so .NET falls back to the
// invariant culture whose currency symbol is "¤" -- that's why ".ToString(\"C\")"
// printed "¤1,234.56" instead of "$1,234.56". Building the string ourselves with
// the invariant culture and a literal "$" gives a stable result everywhere.
public static class Money
{
    public static string Usd(decimal amount) =>
        "$" + amount.ToString("N2", CultureInfo.InvariantCulture);

    // Returns null (renders as nothing) when the value is null, so existing
    // "?? \"Not disclosed\"" fallbacks keep working.
    public static string? Usd(decimal? amount) =>
        amount is null ? null : Usd(amount.Value);
}
