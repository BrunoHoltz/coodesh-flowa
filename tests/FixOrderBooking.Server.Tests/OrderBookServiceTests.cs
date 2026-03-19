using FixOrderBooking.Server.Application;
using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace FixOrderBooking.Server.Tests;

[TestFixture]
public sealed class OrderBookServiceTests
{
    private IOrderRepository _repository = null!;
    private OrderBookService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IOrderRepository>();
        _service = new OrderBookService(_repository, NullLogger<OrderBookService>.Instance);
    }

    [Test]
    public void CreateOrder_ValidInput_ReturnsSuccess()
    {
        var order = MakeOrder("CL1");
        _repository.Get("CL1").Returns((Order?)null);
        _repository.Create(Arg.Any<Order>()).Returns(order);

        var result = _service.CreateOrder("CL1", "PETR4", OrderSide.Buy, 10, 150m);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.ClOrdId, Is.EqualTo("CL1"));
    }

    [Test]
    public void CreateOrder_DuplicateClOrdId_ReturnsDuplicateError()
    {
        _repository.Get("CL1").Returns(MakeOrder("CL1"));

        var result = _service.CreateOrder("CL1", "PETR4", OrderSide.Buy, 10, 150m);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Duplicate));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CreateOrder_EmptyClOrdId_ReturnsValidationError(string clOrdId)
    {
        var result = _service.CreateOrder(clOrdId, "PETR4", OrderSide.Buy, 10, 150m);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Validation));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CreateOrder_EmptySymbol_ReturnsValidationError(string symbol)
    {
        var result = _service.CreateOrder("CL1", symbol, OrderSide.Buy, 10, 150m);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Validation));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void CreateOrder_NonPositiveQuantity_ReturnsValidationError(decimal qty)
    {
        var result = _service.CreateOrder("CL1", "PETR4", OrderSide.Buy, qty, 150m);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Validation));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void CreateOrder_NonPositivePrice_ReturnsValidationError(decimal price)
    {
        var result = _service.CreateOrder("CL1", "PETR4", OrderSide.Buy, 10, price);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public void CreateOrder_RepositoryThrows_ReturnsInternalError()
    {
        _repository.Get("CL1").Returns((Order?)null);
        _repository.Create(Arg.Any<Order>()).Throws(new Exception("db failure"));

        var result = _service.CreateOrder("CL1", "PETR4", OrderSide.Buy, 10, 150m);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.InternalError));
    }


    [Test]
    public void CancelOrder_ExistingOrder_ReturnsSuccess()
    {
        _repository.Get("CL1").Returns(MakeOrder("CL1"));
        _repository.Remove("CL1").Returns(true);

        var result = _service.CancelOrder("CL1");

        Assert.That(result.IsSuccess, Is.True);
        _repository.Received(1).Remove("CL1");
    }

    [Test]
    public void CancelOrder_OrderNotFound_ReturnsNotFoundError()
    {
        _repository.Get("CL1").Returns((Order?)null);

        var result = _service.CancelOrder("CL1");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.NotFound));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void CancelOrder_EmptyOrigClOrdId_ReturnsValidationError(string origClOrdId)
    {
        var result = _service.CancelOrder(origClOrdId);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public void CancelOrder_RepositoryThrows_ReturnsInternalError()
    {
        _repository.Get("CL1").Returns(MakeOrder("CL1"));
        _repository.Remove("CL1").Throws(new Exception("db failure"));

        var result = _service.CancelOrder("CL1");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.InternalError));
    }

    [Test]
    public void GetActiveOrderBook_ReturnsAllFromRepository()
    {
        var grouped = new Dictionary<string, FixOrderBooking.Server.Domain.OrderBook>
        {
            ["PETR4"] = new() { Symbol = "PETR4", Buy = [MakeOrder("CL1"), MakeOrder("CL2")], Sell = [] }
        };
        _repository.GetActiveOrderBook().Returns(grouped);

        var result = _service.GetActiveOrderBook();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Values.Sum(b => b.Buy.Count + b.Sell.Count), Is.EqualTo(2));
    }

    [Test]
    public void GetActiveOrderBook_RepositoryThrows_ReturnsInternalError()
    {
        _repository.GetActiveOrderBook().Throws(new Exception("db failure"));

        var result = _service.GetActiveOrderBook();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorType, Is.EqualTo(ErrorType.InternalError));
    }

    private static Order MakeOrder(string clOrdId) =>
        new(clOrdId, "PETR4", OrderSide.Buy, 10, 150m);
}
