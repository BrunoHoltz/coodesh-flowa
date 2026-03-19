using FixOrderBooking.Client.Application;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace FixOrderBooking.Client.Tests;

[TestFixture]
public sealed class FixAcceptorApplicationTests
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
            new Symbol("AAPL"),
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
            new Symbol("AAPL"),
            new Side(Side.BUY),
            new TransactTime(DateTime.UtcNow));

        Assert.Throws<InvalidOperationException>(() =>
            acceptor.OnMessage(msg, sessionId));
    }
}
