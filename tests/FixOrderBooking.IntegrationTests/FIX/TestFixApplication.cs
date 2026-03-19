using System.Collections.Concurrent;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace FixOrderBooking.IntegrationTests.FIX;

public sealed class TestFixApplication : MessageCracker, IApplication
{
    private readonly TaskCompletionSource<bool> _logonTcs = new();
    private Session? _session;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ExecutionReport>>
        _execReports = new();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<OrderCancelReject>>
        _cancelRejects = new();

    public void OnCreate(SessionID sessionID)
    {
        _session = Session.LookupSession(sessionID);
    }
    public void OnLogon(SessionID sessionID)
    {
        _session = Session.LookupSession(sessionID);
        _logonTcs.TrySetResult(true);
    }
    public void OnLogout(SessionID sessionID)
    {
        _session = null;
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
    }
    public void FromApp(Message message, SessionID sessionID)
    {
        try { Crack(message, sessionID); }
        catch (UnsupportedMessageType) { }
    }

    public void OnMessage(ExecutionReport report, SessionID sessionID)
    {
        var clOrdId = report.ClOrdID.Value;

        if (_execReports.TryRemove(clOrdId, out var tcs))
            tcs.TrySetResult(report);
    }
    public void OnMessage(OrderCancelReject reject, SessionID sessionID)
    {
        var clOrdId = reject.ClOrdID.Value;

        if (_cancelRejects.TryRemove(clOrdId, out var tcs))
            tcs.TrySetResult(reject);
    }

    public Task WaitForLogonAsync(TimeSpan timeout) =>
        _logonTcs.Task.WaitAsync(timeout);
    public Task<ExecutionReport> WaitForExecReportAsync(string clOrdId, TimeSpan timeout)
    {
        var tcs = _execReports.GetOrAdd(clOrdId, _ => new TaskCompletionSource<ExecutionReport>());
        return tcs.Task.WaitAsync(timeout);
    }
    public Task<OrderCancelReject> WaitForCancelRejectAsync(string clOrdId, TimeSpan timeout)
    {
        var tcs = _cancelRejects.GetOrAdd(clOrdId, _ => new TaskCompletionSource<OrderCancelReject>());
        return tcs.Task.WaitAsync(timeout);
    }

    public void Send(Message message)
    {
        if (_session is null)
            throw new InvalidOperationException("FIX session is not connected.");

        _session.Send(message);
    }
}
