using FixOrderBooking.Server.Domain;
using FixOrderBooking.Server.Infra;
using NUnit.Framework;

namespace FixOrderBooking.Server.Tests;

[TestFixture]
public sealed class InMemoryOrderRepositoryConcurrencyTests
{
    private InMemoryOrderRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = new InMemoryOrderRepository();

    [Test]
    public async Task Create_ConcurrentRequests_AllOrdersStored()
    {
        const int scopes = 10;
        const int perScope = 100;

        var tasks = Enumerable.Range(0, scopes).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < perScope; i++)
                _repository.Create(MakeOrder($"CL-{t}-{i}"));
        }));

        await Task.WhenAll(tasks);

        var totalCount = _repository.GetActiveOrderBook().Values.Sum(b => b.Buy.Count + b.Sell.Count);
        Assert.That(totalCount, Is.EqualTo(scopes * perScope));
    }

    [Test]
    public async Task Create_ConcurrentRequests_NoExceptionsThrown()
    {
        var tasks = Enumerable.Range(0, 500).Select(i =>
            Task.Run(() => _repository.Create(MakeOrder($"CL-{i}"))));

        Assert.DoesNotThrowAsync(() => Task.WhenAll(tasks));
    }

    [Test]
    public async Task Remove_ConcurrentRequests_ExactlyOneSucceeds()
    {
        _repository.Create(MakeOrder("CL1"));

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => _repository.Remove("CL1"))));

        Assert.That(results.Count(r => r), Is.EqualTo(1));
    }

    [Test]
    public async Task Remove_ConcurrentUniqueOrders_AllSucceed()
    {
        const int count = 200;
        for (int i = 0; i < count; i++)
            _repository.Create(MakeOrder($"CL-{i}"));

        var tasks = Enumerable.Range(0, count).Select(i =>
            Task.Run(() => _repository.Remove($"CL-{i}")));

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Has.All.True);
        var totalCount = _repository.GetActiveOrderBook().Values.Sum(b => b.Buy.Count + b.Sell.Count);
        Assert.That(totalCount, Is.Zero);
    }

    [Test]
    public async Task CreateAndRemove_Concurrent_NoExceptionsThrown()
    {
        const int count = 200;
        for (int i = 0; i < count; i++)
            _repository.Create(MakeOrder($"CL-{i}"));

        var creates = Enumerable.Range(count, count).Select(i =>
            Task.Run(() => { _repository.Create(MakeOrder($"CL-{i}")); }));

        var removes = Enumerable.Range(0, count).Select(i =>
            Task.Run(() => { _repository.Remove($"CL-{i}"); }));

        Assert.DoesNotThrowAsync(() => Task.WhenAll(creates.Concat(removes)));
    }

    [Test]
    public async Task GetActiveOrderBook_DuringConcurrentModification_DoesNotThrow()
    {
        const int count = 100;
        for (int i = 0; i < count; i++)
            _repository.Create(MakeOrder($"CL-{i}"));

        var readers = Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => { for (int i = 0; i < 50; i++) _repository.GetActiveOrderBook(); }));

        var writers = Enumerable.Range(count, 50).Select(i =>
            Task.Run(() => _repository.Create(MakeOrder($"CL-{i}"))));

        Assert.DoesNotThrowAsync(() => Task.WhenAll(readers.Concat(writers)));
    }

    private static Order MakeOrder(string clOrdId) =>
        new(clOrdId, "PETR4", OrderSide.Buy, 10, 150m);
}
