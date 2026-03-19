using FixOrderBooking.Client.Application;

namespace FixOrderBooking.Client.Infra;

internal static class OrderEndpoints
{
    internal static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/snapshot", async (OrdersService service, CancellationToken ct) =>
        {
            var orders = await service.GetOrdersSnapshotAsync(ct);
            return Results.Ok(orders);
        })
        .WithName("GetOrdersSnapshot")
        .Produces<IReadOnlyList<Order>>();

        return app;
    }
}
