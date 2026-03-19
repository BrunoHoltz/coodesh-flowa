using System.Text.Json.Serialization;

namespace FixOrderBooking.Server.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderSide
    {
        Undefined,
        Buy = '1',
        Sell = '2',
    }
}
