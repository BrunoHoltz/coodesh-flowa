using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixOrderBooking.IntegrationTests.FIX;

public sealed class TestFixClient : IDisposable
{
    private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LogonTimeout = TimeSpan.FromSeconds(20);

    private readonly TestFixApplication _app = new();
    private readonly SocketInitiator _initiator;

    public TestFixClient(
        string host = "localhost",
        int port = 5002,
        string senderCompId = "EXTERNALCLIENT",
        string targetCompId = "CLIENT1")
    {
        var storePath = Path.Combine(Path.GetTempPath(), $"fix-test-store-{Guid.NewGuid():N}");

        var cfg = $"""
            [DEFAULT]
            ConnectionType=initiator
            ReconnectInterval=2
            FileStorePath={storePath}
            StartTime=00:00:00
            EndTime=00:00:00
            UseDataDictionary=Y
            DataDictionary=FIX44.xml

            [SESSION]
            BeginString=FIX.4.4
            SenderCompID={senderCompId}
            TargetCompID={targetCompId}
            SocketConnectHost={host}
            SocketConnectPort={port}
            HeartBtInt=30
            """;

        var settings = new SessionSettings(new StringReader(cfg));
        var storeFactory = new FileStoreFactory(settings);

        _initiator = new SocketInitiator(_app, storeFactory, settings,
            loggerFactoryNullable: null);
    }

    public async Task StartAsync()
    {
        _initiator.Start();
        await _app.WaitForLogonAsync(LogonTimeout);
    }

    public void Stop() => _initiator.Stop();

    public async Task<ExecutionReport> SendNewOrderAsync(
        string clOrdId, string symbol, char side,
        decimal quantity, decimal price,
        TimeSpan? timeout = null)
    {
        var wait = _app.WaitForExecReportAsync(clOrdId, timeout ?? DefaultResponseTimeout);

        _app.Send(new NewOrderSingle(
            new ClOrdID(clOrdId),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT))
        {
            OrderQty = new OrderQty(quantity),
            Price = new Price(price),
            TimeInForce = new TimeInForce(TimeInForce.DAY)
        });

        return await wait;
    }

    public async Task<(ExecutionReport? Accepted, OrderCancelReject? Rejected)> SendCancelAsync(
        string clOrdId, string origClOrdId,
        string symbol, char side,
        TimeSpan? timeout = null)
    {
        var t = timeout ?? DefaultResponseTimeout;

        var execWait = _app.WaitForExecReportAsync(clOrdId, t);
        var rejectWait = _app.WaitForCancelRejectAsync(clOrdId, t);

        _app.Send(new OrderCancelRequest(
            new OrigClOrdID(origClOrdId),
            new ClOrdID(clOrdId),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow)));

        var first = await Task.WhenAny(execWait, rejectWait);

        if (first == execWait)
            return (await execWait, null);

        return (null, await rejectWait);
    }
    public void Dispose()
    {
        _initiator.Stop();
        _initiator.Dispose();
    }
}
