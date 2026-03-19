namespace FixOrderBooking.Client.Application;

public record Order(
    string? OrderId,
    string ClOrdId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    string Status,
    DateTime CreatedAt);
