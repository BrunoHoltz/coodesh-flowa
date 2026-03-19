using FixOrderBooking.Server.Domain;
using FixOrderBooking.Server.Infra;
using NUnit.Framework;

namespace FixOrderBooking.Server.Tests;

[TestFixture]
public sealed class InMemoryOrderRepositoryEdgeCaseTests
{
    private InMemoryOrderRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new InMemoryOrderRepository();

    [Test]
    public void Create_GeneratesUniqueOrderIdPerOrder()
    {
        var a = _repository.Create(MakeOrder("CL1"));
        var b = _repository.Create(MakeOrder("CL2"));

        Assert.That(a.OrderId, Is.Not.EqualTo(b.OrderId));
    }

    [Test]
    public void Create_PreservesExistingOrderId()
    {
        var order = new Order("CL1", "PETR4", OrderSide.Buy, 10, 150m, orderId: "PRESET-ID");
        var created = _repository.Create(order);

        Assert.That(created.OrderId, Is.EqualTo("PRESET-ID"));
    }

    [Test]
    public void Create_GeneratedOrderId_IsValidGuid()
    {
        var created = _repository.Create(MakeOrder("CL1"));

        Assert.That(Guid.TryParse(created.OrderId, out _), Is.True);
    }

    [Test]
    public void Remove_CalledTwiceOnSameOrder_SecondCallReturnsFalse()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Remove("CL1");

        Assert.That(_repository.Remove("CL1"), Is.False);
    }

    [Test]
    public void Create_AfterRemove_OrderIsStoredAgain()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Remove("CL1");
        _repository.Create(MakeOrder("CL1"));

        Assert.That(_repository.Get("CL1"), Is.Not.Null);
    }

    [Test]
    public void GetActiveOrderBook_SingleOrder_ReturnsThatOrder()
    {
        var order = _repository.Create(MakeOrder("CL1"));

        var allOrders = _repository.GetActiveOrderBook().Values
            .SelectMany(b => b.Buy.Concat(b.Sell)).ToList();

        Assert.That(allOrders, Has.Count.EqualTo(1));
        Assert.That(allOrders[0].ClOrdId, Is.EqualTo(order.ClOrdId));
    }

    [Test]
    public void GetActiveOrderBook_AfterRemoveAll_HasNoOrders()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Remove("CL1");
        _repository.Remove("CL2");

        var totalCount = _repository.GetActiveOrderBook().Values
            .Sum(b => b.Buy.Count + b.Sell.Count);

        Assert.That(totalCount, Is.Zero);
    }

    [Test]
    public void Get_CaseSensitive_ReturnsNullForDifferentCase()
    {
        _repository.Create(MakeOrder("cl1"));

        Assert.That(_repository.Get("CL1"), Is.Null);
    }

    [Test]
    public void Get_EmptyString_ReturnsNull()
    {
        Assert.That(_repository.Get(string.Empty), Is.Null);
    }

    private static Order MakeOrder(string clOrdId) =>
        new(clOrdId, "PETR4", OrderSide.Buy, 10, 150m);
}
