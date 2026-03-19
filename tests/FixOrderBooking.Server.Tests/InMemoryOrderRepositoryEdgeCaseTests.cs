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
        var order = new Order("CL1", "AAPL", OrderSide.Buy, 10, 150m, orderId: "PRESET-ID");
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
    public void FindActive_SingleOrder_ReturnsThatOrder()
    {
        var order = _repository.Create(MakeOrder("CL1"));

        var active = _repository.FindActive();

        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ClOrdId, Is.EqualTo(order.ClOrdId));
    }

    [Test]
    public void FindActive_AfterRemoveAll_ReturnsEmpty()
    {
        _repository.Create(MakeOrder("CL1"));
        _repository.Create(MakeOrder("CL2"));
        _repository.Remove("CL1");
        _repository.Remove("CL2");

        Assert.That(_repository.FindActive(), Is.Empty);
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
        new(clOrdId, "AAPL", OrderSide.Buy, 10, 150m);
}
