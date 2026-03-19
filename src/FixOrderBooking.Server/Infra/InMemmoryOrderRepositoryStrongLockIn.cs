using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;
using QuickFix.Fields;

namespace FixOrderBooking.Server.Infra
{
    public class InMemmoryOrderRepositoryStrongLockIn : IOrderRepository
    {
        // Fast lookup
        private readonly Dictionary<string, Order> _ordersById = [];
        // O(1) removal on Cancel
        private readonly Dictionary<string, LinkedListNode<Order>> _orderNodes = [];

        // Symbol -> Price -> OrderBook
        private readonly Dictionary<string, OrderBook> _orderBooksBySymbol = [];

        public Order Create(Order order)
        {
            order.GenerateOrderId();
            _ordersById[order.ClOrdId!] = order;

            if (!_orderBooksBySymbol.TryGetValue(order.Symbol, out var book))
            {
                book = new OrderBook();
                _orderBooksBySymbol[order.Symbol] = book;
            }

            var side = order.Side == OrderSide.Buy ? book.Bids : book.Offers;
            if (!side.TryGetValue(order.Price, out var list))
            {
                list = new LinkedList<Order>();
                side[order.Price] = list;
            }

            var node = list.AddLast(order);
            _orderNodes[order.ClOrdId!] = node;

            return order;
        }

        public bool Remove(string clOrdId)
        {
            if (!_orderNodes.TryGetValue(clOrdId, out var node))
                return false;

            var order = node.Value;
            var book = _orderBooksBySymbol[order.Symbol];
            var side = order.Side == OrderSide.Buy ? book.Bids : book.Offers;

            // O(1)
            var list = side[order.Price];
            list.Remove(node);

            if (list.Count == 0)
                side.Remove(order.Price);

            _orderNodes.Remove(clOrdId);
            _ordersById.Remove(clOrdId);

            return true;
        }

        public Order? Get(string clOrdId) =>
            _ordersById.TryGetValue(clOrdId, out var order) ? order : null;

        public IReadOnlyList<Order> FindActive()
        {
            var result = new List<Order>(_ordersById.Count);

            foreach (var (_, book) in _orderBooksBySymbol)
            {
                // Bids
                foreach (var (_, orders) in book.Bids)
                    result.AddRange(orders);

                // Offers
                foreach (var (_, orders) in book.Offers)
                    result.AddRange(orders);
            }

            return result;
        }
    }
}
