using FixOrderBooking.Server.Domain;

namespace FixOrderBooking.Server.Application.Abstractions;

public interface IOrderBookService
{
    Result<Order> CreateOrder(string clOrdId, string symbol, OrderSide side, decimal quantity, decimal price);
    Result<bool> CancelOrder(string clOrdId);
    Result<IReadOnlyDictionary<string, OrderBook>> GetActiveOrderBook();
}
