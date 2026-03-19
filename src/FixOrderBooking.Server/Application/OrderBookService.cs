using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;

namespace FixOrderBooking.Server.Application;

public sealed class OrderBookService : IOrderBookService
{
    private readonly IOrderRepository _ordersRepository;
    private readonly ILogger<OrderBookService> _logger;

    public OrderBookService(IOrderRepository repository, ILogger<OrderBookService> logger)
    {
        _ordersRepository = repository;
        _logger = logger;
    }

    public Result<Order> CreateOrder(string clOrdId, string symbol, OrderSide side, decimal quantity, decimal price)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clOrdId))
                return Result<Order>.Fail(ErrorType.Validation, "ClOrdId is required.");

            if (string.IsNullOrWhiteSpace(symbol))
                return Result<Order>.Fail(ErrorType.Validation, "Symbol is required.");

            if (!OrderSide.IsValid(side))
                return Result<Order>.Fail(ErrorType.Validation, $"Side '{(char)side}' is not valid. Expected Buy (1) or Sell (2).");

            if (quantity <= 0)
                return Result<Order>.Fail(ErrorType.Validation, "Quantity must be greater than zero.");

            if (price <= 0)
                return Result<Order>.Fail(ErrorType.Validation, "Price must be greater than zero.");

            var existing = _ordersRepository.Get(clOrdId);
            if (existing is not null)
                return Result<Order>.Fail(ErrorType.Duplicate, $"Order with ClOrdId '{clOrdId}' already exists.");

            var order = new Order(clOrdId, symbol, side, quantity, price);
            var created = _ordersRepository.Create(order);

            return Result<Order>.Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating order ClOrdId={ClOrdId}", clOrdId);
            return Result<Order>.Fail(ErrorType.InternalError, "An internal error occurred while creating the order.");
        }
    }

    public Result<bool> CancelOrder(string origClOrdId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(origClOrdId))
                return Result<bool>.Fail(ErrorType.Validation, "OrigClOrdId is required.");

            var order = _ordersRepository.Get(origClOrdId);
            if (order is null)
                return Result<bool>.Fail(ErrorType.NotFound, $"Order '{origClOrdId}' not found.");

            _ordersRepository.Remove(origClOrdId);
            return Result<bool>.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cancelling order OrigClOrdId={OrigClOrdId}", origClOrdId);
            return Result<bool>.Fail(ErrorType.InternalError, "An internal error occurred while cancelling the order.");
        }
    }

    public Result<IReadOnlyList<Order>> GetActiveOrders()
    {
        try
        {
            var orders = _ordersRepository.FindActive();
            return Result<IReadOnlyList<Order>>.Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving active orders");
            return Result<IReadOnlyList<Order>>.Fail(ErrorType.InternalError, "An internal error occurred while retrieving active orders.");
        }
    }
}
