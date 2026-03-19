using FixOrderBooking.Client.FIX;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace FixOrderBooking.Client.Tests;

[TestFixture]
public sealed class ConsolidatedTests
{
    private FixClientApplication _initiator = null!;

    [SetUp]
    public void SetUp()
    {
        _initiator = new FixClientApplication(NullLogger<FixClientApplication>.Instance);
    }

    [Test]
    public void Constructor_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            _ = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance));
    }

    [Test]
    public void Constructor_MultipleInstances_EachSubscribeIndependently()
    {
        Assert.DoesNotThrow(() =>
        {
            _ = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
            _ = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
        });
    }

    [Test]
    public void OnMessage_NewOrderSingle_ThrowsWhenSessionNotConnected()
    {
        var acceptor = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "EXTERNALCLIENT");

        var msg = new NewOrderSingle(
            new ClOrdID("CL1"),
            new Symbol("PETR4"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT))
        {
            OrderQty = new OrderQty(10),
            Price = new Price(150m)
        };

        // Session is not connected — DispatchNewOrder throws InvalidOperationException
        Assert.Throws<InvalidOperationException>(() =>
            acceptor.OnMessage(msg, sessionId));
    }

    [Test]
    public void OnMessage_OrderCancelRequest_ThrowsWhenSessionNotConnected()
    {
        var acceptor = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "EXTERNALCLIENT");

        var msg = new OrderCancelRequest(
            new OrigClOrdID("CL1"),
            new ClOrdID("CL2"),
            new Symbol("PETR4"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow));

        Assert.Throws<InvalidOperationException>(() =>
            acceptor.OnMessage(msg, sessionId));
    }

    [Test]
    public void DispatchNewOrder_NoSession_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            _initiator.DispatchNewOrder("CL1", "PETR4", Side.BUY, 10, 150m));

    [Test]
    public void DispatchCancelOrder_NoSession_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            _initiator.DispatchCancelOrder("CL2", "CL1", "PETR4", Side.BUY));

    [Test]
    public void OnMessage_ExecutionReport_FiresExecReportReceivedEvent()
    {
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "SERVER1");
        ExecutionReport? received = null;
        _initiator.ExecReportReceived += (r, _) => received = r;

        var report = new ExecutionReport(
            new OrderID("ORD1"), new ExecID("EXEC1"),
            new ExecType(ExecType.NEW), new OrdStatus(OrdStatus.NEW),
            new Symbol("PETR4"), new Side(Side.BUY),
            new LeavesQty(10), new CumQty(0), new AvgPx(150m))
        {
            ClOrdID = new ClOrdID("CL1"),
            OrderQty = new OrderQty(10),
            Price = new Price(150m)
        };

        _initiator.OnMessage(report, sessionId);

        Assert.That(received, Is.SameAs(report));
    }

    [Test]
    public void OnMessage_OrderCancelReject_FiresCancelRejectReceivedEvent()
    {
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "SERVER1");
        OrderCancelReject? received = null;
        _initiator.CancelRejectReceived += (r, _) => received = r;

        var reject = new OrderCancelReject(
            new OrderID("NONE"), new ClOrdID("CL2"), new OrigClOrdID("CL1"),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST));

        _initiator.OnMessage(reject, sessionId);

        Assert.That(received, Is.SameAs(reject));
    }

    [Test]
    public void RouteExecReport_UnknownClOrdId_DoesNotThrow()
    {
        _ = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "SERVER1");

        var report = new ExecutionReport(
            new OrderID("ORD1"), new ExecID("EXEC1"),
            new ExecType(ExecType.NEW), new OrdStatus(OrdStatus.NEW),
            new Symbol("PETR4"), new Side(Side.BUY),
            new LeavesQty(10), new CumQty(0), new AvgPx(0m))
        { ClOrdID = new ClOrdID("UNKNOWN") };

        Assert.DoesNotThrow(() => _initiator.OnMessage(report, sessionId));
    }

    [Test]
    public void RouteCancelReject_UnknownClOrdId_DoesNotThrow()
    {
        _ = new FixAcceptorApplication(_initiator, NullLogger<FixAcceptorApplication>.Instance);
        var sessionId = new QuickFix.SessionID("FIX.4.4", "CLIENT1", "SERVER1");

        var reject = new OrderCancelReject(
            new OrderID("NONE"), new ClOrdID("UNKNOWN"), new OrigClOrdID("UNKNOWN"),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST));

        Assert.DoesNotThrow(() => _initiator.OnMessage(reject, sessionId));
    }
}
