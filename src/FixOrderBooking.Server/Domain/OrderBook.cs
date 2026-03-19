namespace FixOrderBooking.Server.Domain
{
    public class OrderBook
    {
        public string Symbol { get; set; } = string.Empty;
        public IReadOnlyList<Order> Sell { get; set; } = [];
        public IReadOnlyList<Order> Buy { get; set; } = [];
    }
}
