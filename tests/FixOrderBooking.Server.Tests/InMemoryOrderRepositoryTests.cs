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
    public void FindActive_EmptyRepository_ReturnsEmptyList()
    {
        Assert.That(_repository.FindActive(), Is.Empty);
    }

    [Test]
    public void FindActive_ReturnsAllCreatedOrders()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Create(MakeOrder("CL3"));

        Assert.That(_repository.FindActive(), Has.Count.EqualTo(3));
    }

    [Test]
    public void FindActive_ExcludesRemovedOrders()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Remove("CL1");

        Assert.That(_repository.FindActive(), Has.Count.EqualTo(1));
    }

    [Test]
    public void FindActive_ReturnsOrdersSortedByCreatedAt()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));

        var active = _repository.FindActive();

        Assert.That(active[0].ClOrdId, Is.EqualTo("CL1"));
        Assert.That(active[1].ClOrdId, Is.EqualTo("CL2"));
    }

    private static Order MakeOrder(string clOrdId) =>
        new(clOrdId, "AAPL", OrderSide.Buy, 10, 150m);
}
