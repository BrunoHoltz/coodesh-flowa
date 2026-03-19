namespace FixOrderBooking.IntegrationTests;

/// <summary>
/// Shared test data generation utilities used across all integration and acceptance tests.
/// </summary>
internal static class TestData
{
    private static readonly Random Rng = Random.Shared;

    public static readonly string[] WellKnownSymbols =
        ["ABEV3", "BBAS3", "PRIO3", "PETR4", "ITUB4", "BPAC11"];

    public static string NewId() => Guid.CreateVersion7().ToString();

    public static string RandomSymbol() =>
        WellKnownSymbols[Rng.Next(WellKnownSymbols.Length)];

    public static string UniqueSymbol(string prefix = "TEST")
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"{prefix}{suffix}";
    }

    public static decimal RandomPrice(decimal min = 10m, decimal max = 1_000m) =>
        Math.Round(min + (decimal)(Rng.NextDouble() * (double)(max - min)), 2);

    public static decimal RandomQuantity(int min = 1, int max = 100) =>
        Rng.Next(min, max + 1);

    public static class Sides
    {
        /// <summary>FIX Side = Buy (tag 54, value '1').</summary>
        public const char Buy = '1';

        /// <summary>FIX Side = Sell (tag 54, value '2').</summary>
        public const char Sell = '2';

        /// <summary>Returns Buy or Sell at random.</summary>
        public static char Random() => Rng.Next(2) == 0 ? Buy : Sell;
    }
}
public record Order(
    string? OrderId,
    string ClOrdId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    string Status,
    DateTime CreatedAt);

public record OrderBook(
    string Symbol,
    IReadOnlyList<Order> Buy,
    IReadOnlyList<Order> Sell);
