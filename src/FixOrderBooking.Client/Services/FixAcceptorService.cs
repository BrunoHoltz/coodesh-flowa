using FixOrderBooking.Client.FIX;
using QuickFix;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixOrderBooking.Client.Services;

public class FixAcceptorService : IHostedService, IDisposable
{
    private readonly FixAcceptorApplication _app;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FixAcceptorService> _logger;
    private readonly IConfiguration _config;
    private ThreadedSocketAcceptor? _acceptor;

    public FixAcceptorService(
        FixAcceptorApplication app,
        ILoggerFactory loggerFactory,
        ILogger<FixAcceptorService> logger,
        IConfiguration config)
    {
        _app = app;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _config = config;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = _config["FIX:AcceptorPort"] ?? "5002";
        var senderCompId = _config["FIX:AcceptorSenderCompId"] ?? "CLIENT1";
        var targetCompId = _config["FIX:AcceptorTargetCompId"] ?? "EXTERNALCLIENT";
        var heartBtInt = _config["FIX:HeartBtInt"] ?? "30";

        var cfg = $"""
            [DEFAULT]
            ConnectionType=acceptor
            ResetOnLogon=Y
            StartTime=00:00:00
            EndTime=00:00:00
            UseDataDictionary=Y
            DataDictionary=FIX44.xml

            [SESSION]
            BeginString=FIX.4.4
            SenderCompID={senderCompId}
            TargetCompID={targetCompId}
            SocketAcceptPort={port}
            HeartBtInt={heartBtInt}
            """;

        var settings = new SessionSettings(new StringReader(cfg));
        var storeFactory = new MemoryStoreFactory();

        _acceptor = new ThreadedSocketAcceptor(_app, storeFactory, settings, loggerFactory: _loggerFactory);
        _acceptor.Start();

        _logger.LogInformation(
            "FIX acceptor started — port={Port} sender={Sender} target={Target}",
            port, senderCompId, targetCompId);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _acceptor?.Stop();
        _logger.LogInformation("FIX acceptor stopped.");
        return Task.CompletedTask;
    }

    public void Dispose() => _acceptor?.Dispose();
}
