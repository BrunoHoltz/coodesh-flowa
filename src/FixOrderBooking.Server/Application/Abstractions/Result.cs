namespace FixOrderBooking.Server.Application.Abstractions
{
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T Value { get; }
        public ErrorType ErrorType { get; }
        public string? ErrorMessage { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            ErrorType = default;
            ErrorMessage = null;
        }

        private Result(ErrorType errorType, string message)
        {
            IsSuccess = false;
            Value = default!;
            ErrorType = errorType;
            ErrorMessage = message;
        }

        public static Result<T> Ok(T value)
            => new(value);

        public static Result<bool> Ok()
            => new(true);

        public static Result<T> Fail(ErrorType type, string message)
            => new(type, message);
    }
    public sealed class Result
    {
        public static Result<T> Ok<T>(T value)
            => Result<T>.Ok(value);

        public static Result<T> Fail<T>(ErrorType type, string message)
            => Result<T>.Fail(type, message);
    }
}
