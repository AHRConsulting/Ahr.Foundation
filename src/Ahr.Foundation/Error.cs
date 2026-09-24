namespace Ahr.Foundation;

/// <summary>Represents a built-in result failure.</summary>
public readonly record struct Error
{

    /// <summary>Initializes an error with a non-null message and optional exception.</summary>
    public Error(string message, Exception? exception = null)
    {
        Message = Guard.NotNull(message, nameof(message));
        Exception = exception;
    }

    /// <summary>Gets the human-readable failure message.</summary>
    public string Message
    {
        get;
        init => field = Guard.NotNull(value, nameof(value));
    }

    /// <summary>Gets the underlying exception, when one exists.</summary>
    public Exception? Exception
    {
        get; init;
    }

    /// <summary>Creates an error from a message.</summary>
    public static Error FromMessage(string message) => new(message);

    /// <summary>Creates an error from a non-null exception.</summary>
    public static Error FromException(Exception exception)
    {
        _ = Guard.NotNull(exception, nameof(exception));
        return new(exception.Message, exception);
    }

    /// <inheritdoc />
    public override string ToString() => Message;
}
