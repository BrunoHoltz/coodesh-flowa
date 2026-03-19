namespace FixOrderBooking.Server.Domain
{
    public class OrderBook
    {
        public SortedDictionary<decimal, LinkedList<Order>> Bids { get; set; } = [];
        public SortedDictionary<decimal, LinkedList<Order>> Offers { get; set; } = [];
    }
}
