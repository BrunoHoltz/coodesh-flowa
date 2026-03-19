using FixOrderBooking.Client.Application;
using QuickFix;
using QuickFix.Store;
using QuickFix.Transport;

namespace FixOrderBooking.Client.Infra;

public sealed class FixClientService : IHostedService, IDisposable
{
    private readonly FixClientApplication _app;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FixClientService> _logger;
    private readonly IConfiguration _config;
    private SocketInitiator? _initiator;

    public FixClientService(
        FixClientApplication app,
        ILoggerFactory loggerFactory,
        ILogger<FixClientService> logger,
        IConfiguration config)
    {
        _app = app;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _config = config;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var host = _config["FIX:ServerHost"] ?? "localhost";
        var port = _config["FIX:ServerPort"] ?? "5001";
        var senderCompId = _config["FIX:SenderCompId"] ?? "CLIENT1";
        var targetCompId = _config["FIX:TargetCompId"] ?? "SERVER1";
        var heartBtInt = _config["FIX:HeartBtInt"] ?? "30";
        var reconnectInterval = _config["FIX:ReconnectInterval"] ?? "5";

        var cfg = $"""
            [DEFAULT]
            ConnectionType=initiator
            ReconnectInterval={reconnectInterval}
            FileStorePath=store
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
            HeartBtInt={heartBtInt}
            """;

        var settings = new SessionSettings(new StringReader(cfg));
        var storeFactory = new FileStoreFactory(settings);

        _initiator = new SocketInitiator(_app, storeFactory, settings, loggerFactoryNullable: _loggerFactory);
        _initiator.Start();

        _logger.LogInformation(
            "FIX initiator started — host={Host} port={Port} sender={Sender} target={Target}",
            host, port, senderCompId, targetCompId);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _initiator?.Stop();
        _logger.LogInformation("FIX initiator stopped.");
        return Task.CompletedTask;
    }

    public void Dispose() => _initiator?.Dispose();
}
