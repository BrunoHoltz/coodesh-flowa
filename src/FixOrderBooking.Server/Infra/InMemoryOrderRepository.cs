using System.Collections.Concurrent;
using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;

namespace FixOrderBooking.Server.Infra;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Order Create(Order order)
    {
        order.GenerateOrderId();
        _orders[order.ClOrdId] = order;
        return order;
    }

    public Order? Get(string clOrdId) =>
        _orders.TryGetValue(clOrdId, out var order) ? order : null;

    public bool Remove(string clOrdId) =>
        _orders.TryRemove(clOrdId, out _);

    public IReadOnlyList<Order> FindActive() =>
        _orders.Values.OrderBy(o => o.CreatedAt).ToList();
}
