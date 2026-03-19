using System.Net;
using System.Text.Json;
using FixOrderBooking.Client.Application;
using NUnit.Framework;

namespace FixOrderBooking.Client.Tests;

[TestFixture]
public sealed class OrdersServiceTests
{
    [Test]
    public async Task GetOrdersSnapshotAsync_ServerReturnsOrders_ReturnsMappedList()
    {
        var orders = new[]
        {
            new { OrderId = "ORD1", ClOrdId = "CL1", Symbol = "AAPL", Side = "Buy",
                  Quantity = 10m, Price = 150m, Status = "New", CreatedAt = DateTime.UtcNow },
            new { OrderId = "ORD2", ClOrdId = "CL2", Symbol = "MSFT", Side = "Sell",
                  Quantity = 5m,  Price = 300m, Status = "New", CreatedAt = DateTime.UtcNow }
        };

        var service = BuildService(HttpStatusCode.OK, orders);

        var result = await service.GetOrdersSnapshotAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].ClOrdId, Is.EqualTo("CL1"));
        Assert.That(result[1].ClOrdId, Is.EqualTo("CL2"));
    }

    [Test]
    public async Task GetOrdersSnapshotAsync_ServerReturnsEmptyArray_ReturnsEmptyList()
    {
        var service = BuildService(HttpStatusCode.OK, Array.Empty<object>());

        var result = await service.GetOrdersSnapshotAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetOrdersSnapshotAsync_ServerReturnsError_ThrowsHttpRequestException()
    {
        var service = BuildService(HttpStatusCode.InternalServerError, null);

        Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetOrdersSnapshotAsync());
    }
    private static OrdersService BuildService(HttpStatusCode status, object? body)
    {
        var handler = new StubHttpMessageHandler(status, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://server") };
        return new OrdersService(client);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode status, object? body)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);

            if (body is not null)
                response.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json");

            return Task.FromResult(response);
        }
    }
}
