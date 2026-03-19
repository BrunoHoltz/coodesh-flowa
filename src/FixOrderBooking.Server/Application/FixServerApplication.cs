using System.Collections.Concurrent;
using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Domain;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace FixOrderBooking.Server.Application;

public sealed class FixServerApplication : MessageCracker, IApplication
{
    private readonly IOrderBookService _service;
    private readonly ILogger<FixServerApplication> _logger;
    private readonly ConcurrentDictionary<SessionID, Session> _sessions = new();

    public FixServerApplication(IOrderBookService service, ILogger<FixServerApplication> logger)
    {
        _service = service;
        _logger = logger;
    }

    public void OnCreate(SessionID sessionID)
    {
        var session = Session.LookupSession(sessionID)
            ?? throw new ApplicationException($"Session not found: {sessionID}");

        _sessions[sessionID] = session;
        _logger.LogInformation("Session created: {SessionID}", sessionID);
    }

    public void OnLogon(SessionID sessionID) =>
        _logger.LogInformation("Logon: {SessionID}", sessionID);

    public void OnLogout(SessionID sessionID)
    {
        _sessions.TryRemove(sessionID, out _);
        _logger.LogInformation("Logout: {SessionID}", sessionID);
    }

    public void FromAdmin(Message message, SessionID sessionID) { }
    public void ToAdmin(Message message, SessionID sessionID) { }

    public void ToApp(Message message, SessionID sessionID)
    {
        try
        {
            if (message.Header.IsSetField(Tags.PossDupFlag) &&
                message.Header.GetBoolean(Tags.PossDupFlag))
                throw new DoNotSend();
        }
        catch (FieldNotFoundException) { }

        _logger.LogDebug("OUT: {Message}", message.ConstructString());
    }

    public void FromApp(Message message, SessionID sessionID)
    {
        _logger.LogDebug("IN: {Message}", message.ConstructString());

        try
        {
            Crack(message, sessionID);
        }
        catch (UnsupportedMessageType)
        {
            _logger.LogWarning("Unsupported message type received: {MsgType}", message.Header.GetString(Tags.MsgType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error cracking message: {MsgType}", message.Header.GetString(Tags.MsgType));
        }
    }

    public void OnMessage(NewOrderSingle msg, SessionID sessionID)
    {
        var clOrdId = msg.ClOrdID.Value;
        var symbol = msg.Symbol.Value;
        OrderSide side = msg.Side.Value;
        var qty = msg.OrderQty.Value;
        var price = msg.Price.Value;

        _logger.LogInformation(
            "NewOrder: ClOrdID={ClOrdID} {Symbol} Side={Side} Qty={Qty} Price={Price}",
            clOrdId, symbol, (char)side, qty, price);

        var result = _service.CreateOrder(clOrdId, symbol, side, qty, price);

        if (!result.IsSuccess)
        {
            LogFailure("CreateOrder", result.ErrorType, result.ErrorMessage!);

            Send(sessionID, BuildNewOrderReject(
                clOrdId, symbol, side, qty, price,
                result.ErrorType, result.ErrorMessage!));
            return;
        }

        var order = result.Value;
        Send(sessionID, BuildExecReport(
            order.OrderId!, order.ClOrdId, order.Symbol, order.Side,
            order.Quantity, order.Price,
            ExecType.NEW, OrdStatus.NEW,
            leavesQty: order.Quantity, cumQty: 0));
    }

    public void OnMessage(OrderCancelRequest msg, SessionID sessionID)
    {
        var origClOrdId = msg.OrigClOrdID.Value;
        var clOrdId = msg.ClOrdID.Value;
        var symbol = msg.Symbol.Value;
        OrderSide side = msg.Side.Value;

        _logger.LogInformation(
            "CancelRequest: ClOrdID={ClOrdID} OrigClOrdID={OrigClOrdID}",
            clOrdId, origClOrdId);

        var result = _service.CancelOrder(origClOrdId);

        if (!result.IsSuccess)
        {
            LogFailure("CancelOrder", result.ErrorType, result.ErrorMessage!);

            Send(sessionID, BuildCancelReject(
                clOrdId, origClOrdId, result.ErrorType, result.ErrorMessage!));
            return;
        }

        var report = BuildExecReport(
            origClOrdId, clOrdId, symbol, side,
            qty: 0, price: 0,
            ExecType.CANCELED, OrdStatus.CANCELED,
            leavesQty: 0, cumQty: 0);

        report.OrigClOrdID = new OrigClOrdID(origClOrdId);
        Send(sessionID, report);
    }

    private void LogFailure(string operation, ErrorType errorType, string message)
    {
        if (errorType == ErrorType.InternalError)
            _logger.LogError("{Operation} failed — {ErrorType}: {Message}", operation, errorType, message);
        else
            _logger.LogWarning("{Operation} rejected — {ErrorType}: {Message}", operation, errorType, message);
    }

    // FIX 4.4 OrdRejReason (tag 103) values used here:
    //   6  = Duplicate Order
    //   99 = Other
    private static ExecutionReport BuildNewOrderReject(
        string clOrdId, string symbol, OrderSide side,
        decimal qty, decimal price,
        ErrorType errorType, string text)
    {
        int ordRejReason = errorType switch
        {
            ErrorType.Duplicate => 6,  // Duplicate Order
            _ => 99, // Other
        };

        return new ExecutionReport(
            new OrderID("NONE"),
            new ExecID(Guid.CreateVersion7().ToString()),
            new ExecType(ExecType.REJECTED),
            new OrdStatus(OrdStatus.REJECTED),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0),
            new CumQty(0),
            new AvgPx(0))
        {
            ClOrdID = new ClOrdID(clOrdId),
            OrderQty = new OrderQty(qty),
            Price = new Price(price),
            OrdRejReason = new OrdRejReason(ordRejReason),
            Text = new Text(text)
        };
    }

    // FIX 4.4 CxlRejReason (tag 102) values used here:
    //   0 = Too Late To Cancel (generic / internal)
    //   1 = Unknown Order
    //   6 = Duplicate ClOrdID
    private static OrderCancelReject BuildCancelReject(
        string clOrdId, string origClOrdId,
        ErrorType errorType, string text)
    {
        int cxlRejReason = errorType switch
        {
            ErrorType.NotFound => 1, // Unknown Order
            ErrorType.Duplicate => 6, // Duplicate ClOrdID
            _ => 0, // Too Late To Cancel (covers Validation + InternalError)
        };

        return new OrderCancelReject(
            new OrderID("NONE"),
            new ClOrdID(clOrdId),
            new OrigClOrdID(origClOrdId),
            new OrdStatus(OrdStatus.REJECTED),
            new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST))
        {
            CxlRejReason = new CxlRejReason(cxlRejReason),
            Text = new Text(text)
        };
    }

    private static ExecutionReport BuildExecReport(
        string orderId, string clOrdId, string symbol, OrderSide side,
        decimal qty, decimal price, char execType, char ordStatus,
        decimal leavesQty, decimal cumQty)
    {
        return new ExecutionReport(
            new OrderID(orderId),
            new ExecID(Guid.CreateVersion7().ToString()),
            new ExecType(execType),
            new OrdStatus(ordStatus),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(leavesQty),
            new CumQty(cumQty),
            new AvgPx(cumQty > 0 ? price : 0))
        {
            ClOrdID = new ClOrdID(clOrdId),
            OrderQty = new OrderQty(qty),
            Price = new Price(price)
        };
    }

    private void Send(SessionID sessionID, Message message)
    {
        if (_sessions.TryGetValue(sessionID, out var session))
            session.Send(message);
        else
            _logger.LogError("Cannot send — session {SessionID} not found.", sessionID);
    }
}
