using System.Collections.Concurrent;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace FixOrderBooking.Client.Application;

public sealed class FixAcceptorApplication : MessageCracker, IApplication
{
    private readonly FixClientApplication _initiator;
    private readonly ILogger<FixAcceptorApplication> _logger;
    private readonly ConcurrentDictionary<SessionID, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, SessionID> _pendingByClOrdId = new();

    public FixAcceptorApplication(FixClientApplication initiator, ILogger<FixAcceptorApplication> logger)
    {
        _initiator = initiator;
        _logger = logger;

        _initiator.ExecReportReceived += RouteExecReport;
        _initiator.CancelRejectReceived += RouteCancelReject;
    }

    public void OnCreate(SessionID sessionID)
    {
        var session = Session.LookupSession(sessionID)
            ?? throw new ApplicationException($"Acceptor session not found: {sessionID}");

        _sessions[sessionID] = session;
        _logger.LogInformation("Acceptor session created: {SessionID}", sessionID);
    }
    public void OnLogon(SessionID sessionID) =>
        _logger.LogInformation("Acceptor logon: {SessionID}", sessionID);
    public void OnLogout(SessionID sessionID)
    {
        _sessions.TryRemove(sessionID, out _);
        _logger.LogInformation("Acceptor logout: {SessionID}", sessionID);
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

        _logger.LogDebug("OUT (to external): {Message}", message.ConstructString());
    }
    public void FromApp(Message message, SessionID sessionID)
    {
        _logger.LogDebug("IN (from external): {Message}", message.ConstructString());

        try
        {
            Crack(message, sessionID);
        }
        catch (UnsupportedMessageType)
        {
            _logger.LogWarning("Unsupported message type from external client: {MsgType}",
                message.Header.GetString(Tags.MsgType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from external client: {MsgType}",
                message.Header.GetString(Tags.MsgType));
        }
    }

    public void OnMessage(NewOrderSingle msg, SessionID sessionID)
    {
        var clOrdId = msg.ClOrdID.Value;
        _pendingByClOrdId[clOrdId] = sessionID;

        _logger.LogInformation(
            "Received NewOrder from external: ClOrdID={ClOrdID} {Symbol} Side={Side} Qty={Qty} Price={Price}",
            clOrdId, msg.Symbol.Value, msg.Side.Value, msg.OrderQty.Value, msg.Price.Value);

        _initiator.DispatchNewOrder(
            clOrdId,
            msg.Symbol.Value,
            msg.Side.Value,
            msg.OrderQty.Value,
            msg.Price.Value);
    }
    public void OnMessage(OrderCancelRequest msg, SessionID sessionID)
    {
        var clOrdId = msg.ClOrdID.Value;
        _pendingByClOrdId[clOrdId] = sessionID;

        _logger.LogInformation(
            "Received CancelRequest from external: ClOrdID={ClOrdID} OrigClOrdID={OrigClOrdID}",
            clOrdId, msg.OrigClOrdID.Value);

        _initiator.DispatchCancelOrder(
            clOrdId,
            msg.OrigClOrdID.Value,
            msg.Symbol.Value,
            msg.Side.Value);
    }

    private void RouteExecReport(ExecutionReport report, SessionID _)
    {
        var clOrdId = report.ClOrdID.Value;
        var execType = report.ExecType.Value;

        if (!_pendingByClOrdId.TryGetValue(clOrdId, out var externalSession))
            return;

        // Remove mapping only on terminal states
        if (execType is ExecType.REJECTED or ExecType.CANCELED or ExecType.TRADE or ExecType.EXPIRED)
            _pendingByClOrdId.TryRemove(clOrdId, out var _);

        Send(externalSession, report);
    }
    private void RouteCancelReject(OrderCancelReject reject, SessionID _)
    {
        var clOrdId = reject.ClOrdID.Value;

        if (!_pendingByClOrdId.TryRemove(clOrdId, out var externalSession))
            return;

        Send(externalSession, reject);
    }
    private void Send(SessionID sessionID, Message message)
    {
        if (_sessions.TryGetValue(sessionID, out var session))
            session.Send(message);
        else
            _logger.LogWarning("Cannot route response — external session {SessionID} not found.", sessionID);
    }
}
