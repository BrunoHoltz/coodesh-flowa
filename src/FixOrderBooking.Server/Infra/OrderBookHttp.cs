using FixOrderBooking.Server.Application.Abstractions;

namespace FixOrderBooking.Server.Infra;

internal static class OrderBookHttp
{
    internal static IEndpointRouteBuilder MapOrderBookHttp(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/active", (IOrderBookService service) =>
        {
            var result = service.GetActiveOrders();

            if (!result.IsSuccess)
                return MapError(result.ErrorType, result.ErrorMessage!);

            return Results.Ok(result.Value);
        })
        .WithName("GetActiveOrders")
        .Produces<IReadOnlyList<object>>()
        .Produces(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static IResult MapError(ErrorType errorType, string message) => errorType switch
    {
        ErrorType.Validation => Results.BadRequest(new { error = message }),
        ErrorType.NotFound => Results.NotFound(new { error = message }),
        ErrorType.Duplicate => Results.Conflict(new { error = message }),
        ErrorType.InternalError => Results.Problem(message, statusCode: StatusCodes.Status500InternalServerError),
        _ => Results.Problem(message, statusCode: StatusCodes.Status500InternalServerError)
    };
}
