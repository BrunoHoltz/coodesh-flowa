using System.Diagnostics;
using FixOrderBooking.IntegrationTests.FIX;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QuickFix.Fields;

namespace FixOrderBooking.IntegrationTests;

[TestFixture]
[Category("Integration")]
[Category("Load")]
public sealed class LoadTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables("INTEGRATION_")
        .Build();

    private static readonly int Total = Config.GetValue("LoadTest:Latency:TotalRequests", 100_000);
    private static readonly int Warmup = Config.GetValue("LoadTest:Latency:WarmupRequests", 1_000);
    private static readonly double LimitMs = Config.GetValue("LoadTest:Latency:LimitMs", 1.0);

    private TestFixClient _client = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _client = new TestFixClient();
        await _client.StartAsync();
    }

    [OneTimeTearDown]
    public void Stop() => _client.Dispose();

    [Test]
    [Description("SLA gate: 100k NewOrderSingle + ExecutionReport sequencial. Cadeia de chamada FIX inteira com latencia media abaixo de 1 ms.")]
    public async Task NewOrderSingle_MeanEndToEndLatency_MustBeUnder1ms()
    {
        // Warmup
        for (int i = 0; i < Warmup; i++)
            await _client.SendNewOrderAsync(TestData.NewId(), "WARMUP", TestData.Sides.Buy, 1m, 100m);

        // Collect gen2 to speed up iterations
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        // Measure
        var ns = new long[Total];
        int failures = 0;

        for (int i = 0; i < Total; i++)
        {
            long t1 = Stopwatch.GetTimestamp();
            var report = await _client.SendNewOrderAsync(TestData.NewId(), "LATENCY", TestData.Sides.Buy, 1m, 100m);
            long t2 = Stopwatch.GetTimestamp();

            ns[i] = (t2 - t1) * 1_000_000_000L / Stopwatch.Frequency;

            if (report.ExecType.Value != ExecType.NEW)
                failures++;
        }

        // Stats
        double meanMs = ns.Average() / 1_000_000.0;

        var sorted = ns.ToArray();
        Array.Sort(sorted);

        double p50Ms = sorted[(int)(Total * 0.50)] / 1_000_000.0;
        double p95Ms = sorted[(int)(Total * 0.95)] / 1_000_000.0;
        double p99Ms = sorted[(int)(Total * 0.99)] / 1_000_000.0;
        double maxMs = sorted[^1] / 1_000_000.0;

        TestContext.WriteLine(
            $"\n  NewOrderSingle → ExecutionReport  ({Total:N0} requests)\n" +
            $"\n  Media : {meanMs,8:F3} ms   (SLA: < {LimitMs} ms)" +
            $"\n  P50  : {p50Ms,8:F3} ms" +
            $"\n  P95  : {p95Ms,8:F3} ms" +
            $"\n  P99  : {p99Ms,8:F3} ms" +
            $"\n  Max  : {maxMs,8:F3} ms" +
            $"\n  Falhas: {failures} / {Total:N0}");

        Assert.That(failures, Is.Zero,
            $"{failures} de {Total:N0} requests falharam (ExecType != NEW)");

        Assert.That(meanMs, Is.LessThan(LimitMs),
            $"Media de latencia e2e {meanMs:F3} ms excede o limite de {LimitMs} ms. " +
            $"P99: {p99Ms:F3} ms, Max: {maxMs:F3} ms");
    }
}
