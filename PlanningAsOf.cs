using System.Globalization;

namespace MRP.Api;

public static class PlanningAsOf
{
    public static DateTime ResolveStockAsOfUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.UtcNow;

        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
            return DateTime.UtcNow;

        var isDateOnly = raw.Length <= 10 && !raw.Contains('T', StringComparison.OrdinalIgnoreCase);
        if (isDateOnly)
        {
            var localDay = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Local);
            return localDay.AddDays(1).AddTicks(-1).ToUniversalTime();
        }

        return parsed.ToUniversalTime();
    }

    public static DateTime StockOperationsCutoffUtc(string? asOfRaw)
    {
        var resolved = ResolveStockAsOfUtc(asOfRaw);
        var now = DateTime.UtcNow;
        return resolved > now ? now : resolved;
    }

    public static bool ShouldFilterOrdersByDate(string? asOfRaw) => !string.IsNullOrWhiteSpace(asOfRaw);
}
