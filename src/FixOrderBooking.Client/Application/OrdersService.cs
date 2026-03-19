namespace FixOrderBooking.Client.Application;

public sealed class OrdersService(HttpClient httpClient)
{
    public async Task<IReadOnlyList<Order>> GetOrdersSnapshotAsync(CancellationToken ct = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<Order>>("/orders/active", ct);
        return result ?? [];
    }
}
