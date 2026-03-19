using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace FixOrderBooking.Client.FIX;

public class FixClientApplication : MessageCracker, IApplication
{
    private readonly ILogger<FixClientApplication> _logger;
    private Session? _session;
    public event Action<ExecutionReport, SessionID>? ExecReportReceived;
    public event Action<OrderCancelReject, SessionID>? CancelRejectReceived;

    public FixClientApplication(ILogger<FixClientApplication> logger)
    {
        _logger = logger;
    }

    public void OnCreate(SessionID sessionID)
    {
        _session = Session.LookupSession(sessionID)
            ?? throw new ApplicationException($"Session not found: {sessionID}");

        _logger.LogInformation("Session created: {SessionID}", sessionID);
    }
    public void OnLogon(SessionID sessionID) =>
        _logger.LogInformation("Logon: {SessionID}", sessionID);
    public void OnLogout(SessionID sessionID)
    {
        _session = null;
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
            _logger.LogWarning("Unsupported message type: {MsgType}", message.Header.GetString(Tags.MsgType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cracking message: {MsgType}", message.Header.GetString(Tags.MsgType));
        }
    }

    public void OnMessage(ExecutionReport report, SessionID sessionID)
    {
        var execType = report.ExecType.Value;
        var ordStatus = report.OrdStatus.Value;

        if (execType == ExecType.REJECTED)
            _logger.LogWarning(
                "ExecReport REJECTED: ClOrdID={ClOrdID} OrdStatus={OrdStatus} Reason={Reason} Text={Text}",
                report.ClOrdID.Value,
                ordStatus,
                report.IsSetField(Tags.OrdRejReason) ? report.GetString(Tags.OrdRejReason) : "N/A",
                report.IsSetField(Tags.Text) ? report.Text.Value : string.Empty);
        else
            _logger.LogInformation(
                "ExecReport: OrderID={OrderID} ClOrdID={ClOrdID} ExecType={ExecType} OrdStatus={OrdStatus}",
                report.OrderID.Value,
                report.ClOrdID.Value,
                execType,
                ordStatus);

        ExecReportReceived?.Invoke(report, sessionID);
    }
    public void OnMessage(OrderCancelReject reject, SessionID sessionID)
    {
        _logger.LogWarning(
            "CancelReject: ClOrdID={ClOrdID} OrigClOrdID={OrigClOrdID} Reason={Reason} Text={Text}",
            reject.ClOrdID.Value,
            reject.OrigClOrdID.Value,
            reject.IsSetField(Tags.CxlRejReason) ? reject.GetString(Tags.CxlRejReason) : "N/A",
            reject.IsSetField(Tags.Text) ? reject.Text.Value : string.Empty);

        CancelRejectReceived?.Invoke(reject, sessionID);
    }

    public void DispatchNewOrder(string clOrdId, string symbol, char side, decimal quantity, decimal price)
    {
        var session = RequireSession();

        var msg = new NewOrderSingle(
            new ClOrdID(clOrdId),
            new Symbol(symbol),
            new Side(side),
            new TransactTime(DateTime.UtcNow),
            new OrdType(OrdType.LIMIT))
        {
            OrderQty = new OrderQty(quantity),
            Price = new Price(price),
            TimeInForce = new TimeInForce(TimeInForce.DAY)
        };

        session.Send(msg);
        _logger.LogInformation(
            "NewOrder sent: ClOrdID={ClOrdID} {Symbol} Side={Side} Qty={Qty} Price={Price}",
            clOrdId, symbol, side, quantity, price);
    }
    public void DispatchCancelOrder(string newClOrdId, string origClOrdId, string symbol, char Side)
    {
        var session = RequireSession();

        var msg = new OrderCancelRequest(
            new OrigClOrdID(origClOrdId),
            new ClOrdID(newClOrdId),
            new Symbol(symbol),
            new Side(Side),
            new TransactTime(DateTime.UtcNow));

        session.Send(msg);
        _logger.LogInformation(
            "CancelRequest sent: NewClOrdID={NewClOrdID} OrigClOrdID={OrigClOrdID}",
            newClOrdId, origClOrdId);
    }

    private Session RequireSession() =>
        _session ?? throw new InvalidOperationException("FIX session is not connected.");
}
