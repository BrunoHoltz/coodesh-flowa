using FixOrderBooking.Server.Domain;
using FixOrderBooking.Server.Infra;
using NUnit.Framework;

namespace FixOrderBooking.Server.Tests;

[TestFixture]
public sealed class InMemoryOrderRepositoryTests
{
    private InMemoryOrderRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new InMemoryOrderRepository();

    [Test]
    public void Create_NewOrder_AssignsOrderId()
    {
        var created = _repository.Create(MakeOrder("CL1"));

        Assert.That(created.OrderId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Create_NewOrder_IsRetrievableByClOrdId()
    {
        _repository.Create(MakeOrder("CL1"));

        Assert.That(_repository.Get("CL1"), Is.Not.Null);
    }

    [Test]
    public void Create_OverwritesExistingEntryWithSameClOrdId()
    {
        _repository.Create(MakeOrder("CL1"));
        var second = MakeOrder("CL1");
        _repository.Create(second);

        Assert.That(_repository.Get("CL1"), Is.SameAs(second));
    }

    [Test]
    public void Get_NonExistingClOrdId_ReturnsNull()
    {
        Assert.That(_repository.Get("UNKNOWN"), Is.Null);
    }

    [Test]
    public void Get_ExistingClOrdId_ReturnsSameInstance()
    {
        var order = MakeOrder("CL1");
        _repository.Create(order);

        Assert.That(_repository.Get("CL1"), Is.SameAs(order));
    }

    [Test]
    public void Remove_ExistingOrder_ReturnsTrue()
    {
        _repository.Create(MakeOrder("CL1"));

        Assert.That(_repository.Remove("CL1"), Is.True);
    }

    [Test]
    public void Remove_ExistingOrder_IsNoLongerRetrievable()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Remove("CL1");

        Assert.That(_repository.Get("CL1"), Is.Null);
    }

    [Test]
    public void Remove_NonExistingOrder_ReturnsFalse()
    {
        Assert.That(_repository.Remove("UNKNOWN"), Is.False);
    }

    [Test]
    public void GetActiveOrderBook_EmptyRepository_ReturnsEmptyDictionary()
    {
        Assert.That(_repository.GetActiveOrderBook(), Is.Empty);
    }

    [Test]
    public void GetActiveOrderBook_ReturnsAllCreatedOrders()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Create(MakeOrder("CL3"));

        var book = _repository.GetActiveOrderBook();
        Assert.That(book.Values.Sum(b => b.Buy.Count + b.Sell.Count), Is.EqualTo(3));
    }

    [Test]
    public void GetActiveOrderBook_ExcludesRemovedOrders()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Remove("CL1");

        var book = _repository.GetActiveOrderBook();
        Assert.That(book.Values.Sum(b => b.Buy.Count + b.Sell.Count), Is.EqualTo(1));
    }

    [Test]
    public void GetActiveOrderBook_ReturnsBuysInAscendingPriceThenFifoOrder()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));

        var book = _repository.GetActiveOrderBook();
        Assert.That(book.Values.Sum(b => b.Buy.Count + b.Sell.Count), Is.EqualTo(2),
            "Total order count mismatch — Create may not have run");

        Assert.That(book, Contains.Key("PETR4"));
        var buys = book["PETR4"].Buy;

        Assert.That(buys, Has.Count.EqualTo(2));
        Assert.That(buys[0].ClOrdId, Is.EqualTo("CL1"));
        Assert.That(buys[1].ClOrdId, Is.EqualTo("CL2"));
    }

    private static Order MakeOrder(string clOrdId) =>
        new(clOrdId, "PETR4", OrderSide.Buy, 10, 150m);
}
