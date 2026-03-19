using FixOrderBooking.Server.Domain;

namespace FixOrderBooking.Server.Application.Abstractions;

public interface IOrderRepository
{
    Order Create(Order order);
    Order? Get(string clOrdId);
    bool Remove(string clOrdId);
    IReadOnlyDictionary<string, OrderBook> GetActiveOrderBook();
}
