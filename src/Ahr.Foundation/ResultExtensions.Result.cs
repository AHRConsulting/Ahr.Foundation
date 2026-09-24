namespace Ahr.Foundation;

/// <summary>Explicit factory-style extension methods for results.</summary>
public static partial class ResultExtensions
{
    /// <summary>Creates a payload-free failure.</summary>
    public static Result ToFailure(this Error error) => Result.Failure(error);

    /// <summary>Creates a payload-free failure from a message.</summary>
    public static Result ToFailure(this string message) => Result.Failure(Error.FromMessage(message));

    /// <summary>Creates a payload-free failure from an exception.</summary>
    public static Result ToFailure(this Exception exception) => Result.Failure(Error.FromException(exception));

    /// <summary>Creates a payload-free failure from an exception and message.</summary>
    public static Result ToFailure(this Exception exception, string message)
    {
        _ = Guard.NotNull(exception, nameof(exception));
        return Result.Failure(new Error(message, exception));
    }
}
