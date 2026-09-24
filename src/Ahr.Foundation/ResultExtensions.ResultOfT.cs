namespace Ahr.Foundation;

public static partial class ResultExtensions
{
    /// <summary>Creates a successful built-in error result.</summary>
    public static Result<T> ToSuccess<T>(this T value) => Result<T>.Success(value);

    /// <summary>Creates a value failure.</summary>
    public static Result<T> ToFailure<T>(this Error error) => Result<T>.Failure(error);

    /// <summary>Creates a value failure from a message.</summary>
    public static Result<T> ToFailure<T>(this string message) => Result<T>.Failure(Error.FromMessage(message));

    /// <summary>Creates a value failure from an exception.</summary>
    public static Result<T> ToFailure<T>(this Exception exception) => Result<T>.Failure(Error.FromException(exception));

    /// <summary>Creates a value failure from an exception and message.</summary>
    public static Result<T> ToFailure<T>(this Exception exception, string message)
    {
        _ = Guard.NotNull(exception, nameof(exception));
        return Result<T>.Failure(new Error(message, exception));
    }
}
