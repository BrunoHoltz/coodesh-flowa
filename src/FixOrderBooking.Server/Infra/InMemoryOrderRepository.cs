using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;

namespace FixOrderBooking.Server.Infra
{
    public class InMemoryOrderRepository : IOrderRepository
    {
        private readonly object _lock = new();

        // Fast lookup
        private readonly Dictionary<string, Order> _ordersById = [];
        // O(1) removal on Cancel
        private readonly Dictionary<string, LinkedListNode<Order>> _orderNodes = [];

        // Symbol -> Price -> OrderBook
        struct SortedBookStore()
        {
            public SortedDictionary<decimal, LinkedList<Order>> Bids = [];
            public SortedDictionary<decimal, LinkedList<Order>> Offers = [];
        }
        private readonly Dictionary<string, SortedBookStore> _orderBookStoreBySymbol = [];

        public Order Create(Order order)
        {
            order.GenerateOrderId();

            lock (_lock)
            {
                _ordersById[order.ClOrdId!] = order;

                if (!_orderBookStoreBySymbol.TryGetValue(order.Symbol, out var book))
                {
                    book = new SortedBookStore();
                    _orderBookStoreBySymbol[order.Symbol] = book;
                }

                var side = order.Side == OrderSide.Buy ? book.Bids : book.Offers;
                if (!side.TryGetValue(order.Price, out var list))
                {
                    list = new LinkedList<Order>();
                    side[order.Price] = list;
                }

                var node = list.AddLast(order);
                _orderNodes[order.ClOrdId!] = node;
            }

            return order;
        }

        public bool Remove(string clOrdId)
        {
            lock (_lock)
            {
                if (!_orderNodes.TryGetValue(clOrdId, out var node))
                    return false;

                var order = node.Value;
                var book = _orderBookStoreBySymbol[order.Symbol];
                var side = order.Side == OrderSide.Buy ? book.Bids : book.Offers;

                // O(1) node removal
                var list = side[order.Price];
                list.Remove(node);

                if (list.Count == 0)
                    side.Remove(order.Price);

                _orderNodes.Remove(clOrdId);
                _ordersById.Remove(clOrdId);

                return true;
            }
        }

        public Order? Get(string clOrdId)
        {
            lock (_lock)
                return _ordersById.TryGetValue(clOrdId, out var order) ? order : null;
        }

        public IReadOnlyDictionary<string, OrderBook> GetActiveOrderBook()
        {
            lock (_lock)
            {
                var result = new Dictionary<string, OrderBook>();

                foreach (var (symbol, book) in _orderBookStoreBySymbol)
                {
                    result[symbol] = new OrderBook
                    {
                        Symbol = symbol,
                        Buy = book.Bids.Values.SelectMany(l => l).ToList(),
                        Sell = book.Offers.Values.SelectMany(l => l).ToList()
                    };
                }

                return result;
            }
        }
    }
}
