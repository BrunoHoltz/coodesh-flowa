namespace FixOrderBooking.Client.Services;

public static class OrderBookHttp
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/snapshot", async (HttpClient httpClient, CancellationToken ct) =>
        {
            var response = await httpClient.GetAsync("/orders/active", ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return Results.Content(content, contentType, statusCode: (int)response.StatusCode);
        })
        .WithName("GetOrdersSnapshot");

        return app;
    }
}
