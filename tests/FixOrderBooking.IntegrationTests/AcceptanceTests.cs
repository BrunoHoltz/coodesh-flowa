using FixOrderBooking.IntegrationTests.FIX;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QuickFix.Fields;
using System.Net;
using System.Net.Http.Json;

namespace FixOrderBooking.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("Acceptance")]
public sealed class AcceptanceTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables("INTEGRATION_")
        .Build();

    private TestFixClient _fixClient = null!;
    private HttpClient _http;

    [OneTimeSetUp]
    public async Task StartClients()
    {
        _fixClient = new TestFixClient();
        await _fixClient.StartAsync();

        _http = new()
        {
            BaseAddress = new Uri(
                $"http://{Config["Infra:Endpoints:Client:HTTP:Host"]}:{Config["Infra:Endpoints:Client:HTTP:Port"]}"),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    [OneTimeTearDown]
    public void StopClients() => _fixClient.Dispose();

    [Test]
    [Description("Teste básico de nova ordem. Deve retornar ExecType=NEW com os parâmetros enviados.")]
    public async Task NewOrder_ValidOrder_ReceivesNewExecReport()
    {
        var clOrdId = TestData.NewId();
        var symbol = TestData.WellKnownSymbols[0];
        var side = TestData.Sides.Buy;
        var qty = 10m;
        var price = 150m;

        var report = await _fixClient.SendNewOrderAsync(clOrdId, symbol, side, qty, price);

        Assert.That(report.ExecType.Value, Is.EqualTo(ExecType.NEW));
        Assert.That(report.OrdStatus.Value, Is.EqualTo(OrdStatus.NEW));
        Assert.That(report.Side.Value, Is.EqualTo(side));
        Assert.That(report.ClOrdID.Value, Is.EqualTo(clOrdId));
        Assert.That(report.Symbol.Value, Is.EqualTo(symbol));
        Assert.That(report.OrderQty.Value, Is.EqualTo(qty));
        Assert.That(report.Price.Value, Is.EqualTo(price));
    }

    [Test]
    [Description("Teste de idempotencia. Uma segunda ordem com o ClOrdID existente deve retornar ExecType=REJECTED com OrdRejReason.")]
    public async Task NewOrder_DuplicateClOrdId_ReceivesRejectedExecReport()
    {
        var clOrdId = TestData.NewId();

        // First
        await _fixClient.SendNewOrderAsync(clOrdId, "PETR4", TestData.Sides.Buy, 10m, 150m);
        // Duplicated Id
        var reject = await _fixClient.SendNewOrderAsync(clOrdId, "BOVA11", TestData.Sides.Sell, 1m, 15m);

        Assert.That(reject.ExecType.Value, Is.EqualTo(ExecType.REJECTED));
        Assert.That(reject.OrdStatus.Value, Is.EqualTo(OrdStatus.REJECTED));
        Assert.That(reject.IsSetField(Tags.OrdRejReason), Is.True);
    }

    [Test]
    [Description("Teste de concorrencia. Requests concorrentes devem receber seu ExecReport associado ao ClOrdID enviado.")]
    public async Task NewOrder_MultipleOrders_EachReceivesIndependentReport()
    {
        var id1 = TestData.NewId();
        var id2 = TestData.NewId();
        var id3 = TestData.NewId();

        var t1 = _fixClient.SendNewOrderAsync(id1, TestData.WellKnownSymbols[0], TestData.Sides.Buy, 10m, 150m);
        var t2 = _fixClient.SendNewOrderAsync(id2, TestData.WellKnownSymbols[1], TestData.Sides.Sell, 5m, 300m);
        var t3 = _fixClient.SendNewOrderAsync(id3, TestData.WellKnownSymbols[2], TestData.Sides.Buy, 20m, 200m);

        var reports = await Task.WhenAll(t1, t2, t3);

        Assert.That(reports[0].ClOrdID.Value, Is.EqualTo(id1));
        Assert.That(reports[1].ClOrdID.Value, Is.EqualTo(id2));
        Assert.That(reports[2].ClOrdID.Value, Is.EqualTo(id3));
        Assert.That(reports, Has.All.Matches<QuickFix.FIX44.ExecutionReport>(
            r => r.ExecType.Value == ExecType.NEW));
    }

    [Test]
    [Description("Teste basico de cancelamento. Um OrderCancelRequest em uma ordem viva deve retornar ExecType=CANCELED sem tag OrderCancelReject.")]
    public async Task Cancel_ExistingOrder_ReceivesCanceledExecReport()
    {
        var createId = TestData.NewId();
        var cancelId = TestData.NewId();

        await _fixClient.SendNewOrderAsync(createId, "PETR4", TestData.Sides.Buy, 10m, 150m);

        var (accepted, rejected) = await _fixClient.SendCancelAsync(
            cancelId, createId, "PETR4", TestData.Sides.Buy);

        Assert.That(rejected, Is.Null);
        Assert.That(accepted, Is.Not.Null);
        Assert.That(accepted!.ExecType.Value, Is.EqualTo(ExecType.CANCELED));
        Assert.That(accepted!.OrdStatus.Value, Is.EqualTo(OrdStatus.CANCELED));
        Assert.That(accepted.IsSetField(Tags.CxlRejReason), Is.False);
    }

    [Test]
    [Description("Teste de cancelamento em ordem inexistente. Cancelamento de uma ordem inexistente deve retornar OrderCancelReject com CxlRejReason=1 (Unknown order).")]
    public async Task Cancel_NonExistentOrder_ReceivesCancelReject()
    {
        var (accepted, rejected) = await _fixClient.SendCancelAsync(
            TestData.NewId(), TestData.NewId(), "PETR4", TestData.Sides.Buy);

        Assert.That(accepted, Is.Null);
        Assert.That(rejected, Is.Not.Null);
        Assert.That(rejected!.IsSetField(Tags.CxlRejReason), Is.True);
        Assert.That(rejected!.GetInt(Tags.CxlRejReason), Is.EqualTo(1)); // Unknown order
    }

    [Test]
    [Description("Teste de cancelamento em ordem ja cancelada. Um OrderCancelRequest em uma ordem ja cancelada deve ser rejeitado.")]
    public async Task Cancel_AlreadyCancelledOrder_ReceivesCancelReject()
    {
        var createId = TestData.NewId();
        var cancelId1 = TestData.NewId();
        var cancelId2 = TestData.NewId();

        await _fixClient.SendNewOrderAsync(createId, "PETR4", TestData.Sides.Buy, 10m, 150m);
        await _fixClient.SendCancelAsync(cancelId1, createId, "PETR4", TestData.Sides.Buy);

        var (accepted, rejected) = await _fixClient.SendCancelAsync(
            cancelId2, createId, "PETR4", TestData.Sides.Buy);

        Assert.That(accepted, Is.Null);
        Assert.That(rejected, Is.Not.Null);
    }

    [Test]
    [Description("Teste basico do book de ordens. GET /orders/snapshot deve retornar HTTP 200 OK.")]
    public async Task GetSnapshot_ReturnsOk()
    {
        var response = await _http.GetAsync("/orders/snapshot");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    [Description("Teste de consistencia e2e. Uma ordem enviada deve aparecer no snapshot logo em seguida.")]
    public async Task GetSnapshot_AfterCreatingOrder_ContainsThatOrder()
    {
        var clOrdId = TestData.NewId();
        var symbol = TestData.RandomSymbol();

        await _fixClient.SendNewOrderAsync(clOrdId, symbol, TestData.Sides.Buy, 7m, 99m);

        //Very short delay
        await Task.Delay(100);

        var snapshot = await _http.GetFromJsonAsync<Dictionary<string, OrderBook>>("/orders/snapshot");
        var orders = snapshot?.SelectMany(x => x.Value.Buy.Concat(x.Value.Sell)).ToArray() ?? [];

        Assert.That(orders, Is.Not.Null);
        Assert.That(orders!.Any(o => o.ClOrdId == clOrdId), Is.True,
            $"Nova ordem {clOrdId} deve aparecer no snapshot");
    }

    [Test]
    [Description("Teste de consistencia no cancelamento. Uma ordem cancelada deve deixar de aparecer no snapshot.")]
    public async Task GetSnapshot_AfterCancellingOrder_OrderRemovedFromSnapshot()
    {
        var symbol = TestData.RandomSymbol();
        var createId = TestData.NewId();
        var cancelId = TestData.NewId();

        await _fixClient.SendNewOrderAsync(createId, symbol, TestData.Sides.Buy, 3m, 50m);
        await _fixClient.SendCancelAsync(cancelId, createId, symbol, TestData.Sides.Buy);

        await Task.Delay(100);

        var snapshot = await _http.GetFromJsonAsync<Dictionary<string, OrderBook>>("/orders/snapshot");
        var orders = snapshot?.SelectMany(x => x.Value.Buy.Concat(x.Value.Sell)).ToArray() ?? [];

        Assert.That(orders, Is.Not.Null);
        Assert.That(orders!.Any(o => o.ClOrdId == createId), Is.False,
            $"Ordem cancelada {createId} nao pode aparecer no snapshot");
    }

    [Test]
    [Description("Teste de agrupamento e ordenacao. Ordens devem ser agrupadas por símbolo, lado e ordenados por preco e FIFO.")]
    public async Task GetSnapshot_GroupingAndSorting_AscendingPriceWithinEachSide()
    {
        var symbol1 = "PETR4";
        // PETR4 BUY
        var petr4BuyAt50 = TestData.NewId(); await _fixClient.SendNewOrderAsync(petr4BuyAt50, symbol1, TestData.Sides.Buy, 2m, 50m);
        var petr4BuyAt40 = TestData.NewId(); await _fixClient.SendNewOrderAsync(petr4BuyAt40, symbol1, TestData.Sides.Buy, 1m, 40m);
        // PETR4 SELL
        var petr4SellAt45 = TestData.NewId(); await _fixClient.SendNewOrderAsync(petr4SellAt45, symbol1, TestData.Sides.Sell, 1m, 45m);
        var petr4Sellt58 = TestData.NewId(); await _fixClient.SendNewOrderAsync(petr4Sellt58, symbol1, TestData.Sides.Sell, 1m, 58m);

        var symbol2 = "ABEV3";
        // ABEV3 BUY
        var abev34BuyAt20 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev34BuyAt20, symbol2, TestData.Sides.Buy, 1m, 20m);
        var abev3BuyAt30 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev3BuyAt30, symbol2, TestData.Sides.Buy, 2m, 30m);
        // ABEV3 SELL
        var abev3SellAt25 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev3SellAt25, symbol2, TestData.Sides.Sell, 1m, 25m);
        var abev3SellAt38 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev3SellAt38, symbol2, TestData.Sides.Sell, 1m, 38m);
        var abev3SellAt35 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev3SellAt35, symbol2, TestData.Sides.Sell, 1m, 35m);
        var abev3SellAt35_2 = TestData.NewId(); await _fixClient.SendNewOrderAsync(abev3SellAt35_2, symbol2, TestData.Sides.Sell, 1m, 35m);

        await Task.Delay(100);

        var snapshot = await _http.GetFromJsonAsync<Dictionary<string, OrderBook>>("/orders/snapshot") ?? [];
        var groups = snapshot.Where(x => x.Key == symbol1 || x.Key == symbol2).ToDictionary();
        //Grouping asserts
        Assert.That(groups.Count(), Is.EqualTo(2),
            "Snapshot deve conter os 2 symbols");
        Assert.That(groups?.Sum(s => s.Value.Sell.Count), Is.GreaterThanOrEqualTo(6),
            "Devem existir 6 ou mais Sell Ordens no snapshot");
        Assert.That(groups?.Sum(s => s.Value.Buy.Count), Is.GreaterThanOrEqualTo(4),
            "Devem existir 4 ou mais Buy Ordens no snapshot");

        //Ordering asserts
        // Buy
        int GetBuyIndex(string id) => groups[symbol1].Buy.Select((o, idx) => (o, idx)).First(obj => obj.o.ClOrdId == id).idx;
        Assert.That(GetBuyIndex(petr4BuyAt40), Is.LessThan(GetBuyIndex(petr4BuyAt50)), "PETR4 Buy@40 deve aparecer antes de PETR4 Buy@50");
        // Sell
        int GetSellIndex(string id) => groups[symbol2].Sell.Select((o, idx) => (o, idx)).First(obj => obj.o.ClOrdId == id).idx;
        Assert.That(GetSellIndex(abev3SellAt35), Is.LessThan(GetSellIndex(abev3SellAt38)), "ABEV3 Sell@35 deve aparecer antes de Sell@38");
        Assert.That(GetSellIndex(abev3SellAt35), Is.LessThan(GetSellIndex(abev3SellAt35_2)), "ABEV3 Sell@35 deve aparecer antes de Sell@35_2");
    }
}
