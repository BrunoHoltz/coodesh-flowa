namespace FixOrderBooking.Server.Domain;

public class Order(string clOrdId, string symbol, OrderSide side, decimal quantity, decimal price, string? orderId = null)
{
    public string? OrderId { get; private set; } = orderId;
    public string ClOrdId { get; } = clOrdId;
    public string Symbol { get; } = symbol;
    public decimal Quantity { get; } = quantity;
    public decimal Price { get; } = price;
    public OrderSide Side { get; } = side;
    public OrderStatus Status { get; private set; } = OrderStatus.New;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public void GenerateOrderId()
    {
        if (!string.IsNullOrEmpty(OrderId))
            return;

        OrderId = Guid.CreateVersion7().ToString();
    }
}
