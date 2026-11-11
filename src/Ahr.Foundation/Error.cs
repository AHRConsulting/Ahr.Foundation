namespace Ahr.Foundation;

/// <summary>Represents a built-in result failure.</summary>
public readonly record struct Error
{
    private readonly string? _message;

    /// <summary>Initializes an error with a non-null message and optional exception.</summary>
    public Error(string message, Exception? exception = null)
    {
        _message = Guard.NotNull(message, nameof(message));
        Exception = exception;
    }

    /// <summary>Gets the human-readable failure message; empty for a default-initialized error.</summary>
    public string Message
    {
        get => _message ?? string.Empty;
        init => _message = Guard.NotNull(value, nameof(value));
    }

    /// <summary>Gets the underlying exception, when one exists.</summary>
    public Exception? Exception
    {
        get; init;
    }

    /// <summary>Gets whether the underlying exception is an <see cref="OperationCanceledException"/>, such as a timeout that <c>Result.TryAsync</c> turned into a failure.</summary>
    public bool IsCancellation => Exception is OperationCanceledException;

    /// <summary>Creates an error from a message.</summary>
    public static Error FromMessage(string message) => new(message);

    /// <summary>Creates an error from a non-null exception.</summary>
    public static Error FromException(Exception exception)
    {
        _ = Guard.NotNull(exception, nameof(exception));
        return new(exception.Message, exception);
    }

    /// <summary>Gets whether this error was created by a constructor or factory rather than <c>default</c>.</summary>
    internal bool IsInitialized => _message is not null;

    /// <inheritdoc />
    public override string ToString() => Message;
}
