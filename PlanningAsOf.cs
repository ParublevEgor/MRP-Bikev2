using System.Globalization;

namespace MRP.Api;

// Планирование на дату (снимок)
public static class PlanningAsOf
{
    // Преобразование строки в дату UTC
    // raw - строка с датой
    public static DateTime ResolveStockAsOfUtc(string? raw)
    {
        // Если строка пустая, то возвращаем текущую дату UTC
        if (string.IsNullOrWhiteSpace(raw))
            return DateTime.Now;

        // Преобразование строки в дату UTC
        // Если преобразование не удалось, то возвращаем текущую дату UTC
        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
            return DateTime.Now;

        // Проверка на дату только с датой, без времени
        var isDateOnly = raw.Length <= 10 && !raw.Contains('T', StringComparison.OrdinalIgnoreCase);
        if (isDateOnly)
        {
            // Преобразование даты в локальную дату
            var localDay = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Local);
            // Преобразование локальной даты в UTC
            return localDay.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        // Преобразование даты в UTC
        return parsed.ToUniversalTime();
    }

    // Преобразование строки в дату UTC для операций на складе
    // asOfRaw - строка с датой
    // Возвращает дату UTC
    // Преобразование строки в дату UTC для операций на складе
    public static DateTime StockOperationsCutoffUtc(string? asOfRaw)
    {
        // Преобразование строки в дату UTC
        var resolved = ResolveStockAsOfUtc(asOfRaw);
        var now = DateTime.UtcNow;
        return resolved > now ? now : resolved;
    }

    // Проверка на фильтрацию заказов по дате
    // asOfRaw - строка с датой
    // Возвращает true, если нужно фильтровать заказы по дате
    // Проверка на фильтрацию заказов по дате
    public static bool ShouldFilterOrdersByDate(string? asOfRaw) => !string.IsNullOrWhiteSpace(asOfRaw);
}
