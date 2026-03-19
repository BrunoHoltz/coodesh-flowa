namespace FixOrderBooking.Server.Domain;

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected
}
